using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using Newtonsoft.Json.Linq;
using TMPro;

public class PuzzleManager : MonoBehaviour
{
    [System.Serializable]
    public class PuzzlePartData
    {
        public string word;
        public string prefab;
        public string instruction;
        public string audio_prompt;
        public string victoryMessage;
    }

    [Header("Game Configuration")]
    public string prefabResourceFolder = "Prefabs";
    public ElevenlabsTTS elevenLabsTTS;
    public PuzzleGenerator puzzleGenerator;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip successClip;

    [Header("Puzzle Feedback UI")]
    public GameObject wordDisplayPrefab;
    public Transform memoryCorner;

    private readonly Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();
    private readonly Queue<PuzzlePartData>         puzzleQueue = new Queue<PuzzlePartData>();
    private readonly List<GameObject>              spawnedObjects = new List<GameObject>();

    private PuzzlePartData currentPuzzlePart;
    private string currentNarrativeIntro;
    private bool   isSceneReady = false;

    /* ---------------------------------------------------------------- */
    /* Awake                                                            */
    /* ---------------------------------------------------------------- */
    private void Awake()
    {
        // Ensure AudioSource
        audioSource = audioSource ?? GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        // Find PuzzleGenerator if not assigned
        if (puzzleGenerator == null)
        {
            puzzleGenerator = FindObjectOfType<PuzzleGenerator>();
            if (puzzleGenerator == null)
                Debug.LogError("PuzzleManager: PuzzleGenerator reference missing.");
        }
    }

    /* ---------------------------------------------------------------- */
    /* Scene-ready callback                                             */
    /* ---------------------------------------------------------------- */
    private void Start() => StartCoroutine(RegisterSceneLoadedCallbackWhenReady());

    private IEnumerator RegisterSceneLoadedCallbackWhenReady()
    {
        while (MRUK.Instance == null) yield return null;

        MRUK.Instance.RegisterSceneLoadedCallback(() =>
        {
            Debug.Log("MRUK scene ready.");
            isSceneReady = true;
        });
    }

    /* ---------------------------------------------------------------- */
    /* Prefab Dictionary                                                */
    /* ---------------------------------------------------------------- */
    public void LoadDefinitivePrefabs(HashSet<string> definitivePaths)
    {
        prefabDict.Clear();

        foreach (string fullPath in definitivePaths)
        {
            string simpleName = fullPath.Substring(fullPath.LastIndexOf('/') + 1);
            GameObject prefab = Resources.Load<GameObject>($"{prefabResourceFolder}/{simpleName}");

            if (prefab != null)
            {
                if (!prefabDict.ContainsKey(fullPath))
                    prefabDict.Add(fullPath, prefab);
            }
            else
            {
                Debug.LogError($"Prefab '{simpleName}' not found in Resources/{prefabResourceFolder}.");
            }
        }

        if (prefabDict.Count == 0)
            Debug.LogError("No prefabs loaded into dictionary.");
    }

    /* ---------------------------------------------------------------- */
    /* Cache AI Response                                                */
    /* ---------------------------------------------------------------- */
    public void CachePuzzlesFromResponse(string json)
    {
        puzzleQueue.Clear();

        try
        {
            JObject root = JObject.Parse(json);
            currentNarrativeIntro = root["narrative_intro"]?.ToString();

            foreach (var part in (JArray)root["puzzle_parts"])
            {
                puzzleQueue.Enqueue(new PuzzlePartData
                {
                    word         = part["word"]?.ToString(),
                    prefab       = part["prefab"]?.ToString(),
                    instruction  = part["instruction"]?.ToString(),
                    audio_prompt = part["audio_prompt"]?.ToString()
                });
            }

            string finalPromptText   = root["final_prompt"]?.ToString();
            string expectedAnswer    = root["expected_answer"]?.ToString();
            string selectedPhrase    = root["selected_phrase"]?.ToString();
            string victoryMessageTxt = root["victory_message"]?.ToString();

            if (expectedAnswer != selectedPhrase)
                expectedAnswer = selectedPhrase;

            if (!string.IsNullOrEmpty(finalPromptText))
            {
                puzzleQueue.Enqueue(new PuzzlePartData
                {
                    word          = expectedAnswer,
                    prefab        = "Prefabs/FinalPuzzle",
                    instruction   = finalPromptText,
                    audio_prompt  = finalPromptText,
                    victoryMessage = victoryMessageTxt
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON parse error: {ex.Message}\nRaw: {json}");
        }
    }

    /* ---------------------------------------------------------------- */
    /* Start Sequence                                                   */
    /* ---------------------------------------------------------------- */
    public void StartPuzzleSequence()
    {
        if (prefabDict.Count == 0 &&
            puzzleGenerator != null &&
            puzzleGenerator.definitivePrefabNames.Count > 0)
        {
            LoadDefinitivePrefabs(puzzleGenerator.definitivePrefabNames);
        }

        if (!string.IsNullOrEmpty(currentNarrativeIntro))
            elevenLabsTTS?.Speak(currentNarrativeIntro);

        SpawnNextPuzzlePart();
    }

    /* ---------------------------------------------------------------- */
    /* Spawn Logic                                                      */
    /* ---------------------------------------------------------------- */
    public void SpawnNextPuzzlePart()
    {
        if (!isSceneReady)
        {
            StartCoroutine(RetrySpawn(2f));
            return;
        }

        if (puzzleQueue.Count == 0)
        {
            elevenLabsTTS?.Speak("Félicitations ! Tu as tout terminé.");
            return;
        }

        foreach (var obj in spawnedObjects) Destroy(obj);
        spawnedObjects.Clear();

        currentPuzzlePart = puzzleQueue.Dequeue();
        string prefabPath = currentPuzzlePart.prefab;

        if (!prefabDict.TryGetValue(prefabPath, out GameObject prefabToSpawn))
        {
            Debug.LogError($"Prefab path '{prefabPath}' missing from dictionary.");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition();
        GameObject spawned = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        spawnedObjects.Add(spawned);

        PuzzleWord pw = spawned.GetComponent<PuzzleWord>();
        if (pw == null)
        {
            Debug.LogWarning($"Prefab '{prefabPath}' lacks PuzzleWord component.");
            return;
        }

        bool isFinal = prefabPath == "Prefabs/FinalPuzzle";
        pw.Initialize(currentPuzzlePart.word, currentPuzzlePart.audio_prompt, null);
        pw.OnWordValidated += (w) =>
        {
            if (isFinal) OnFinalValidationResult(true);
            else         HandleWordValidated(w);
        };

        if (!string.IsNullOrEmpty(currentPuzzlePart.instruction))
            elevenLabsTTS?.Speak(currentPuzzlePart.instruction);

        if (isFinal)
            UIManager.Instance.ShowWordPopup(currentPuzzlePart.instruction);
    }

    /* ---------------------------------------------------------------- */
    /* Validation & Victory                                             */
    /* ---------------------------------------------------------------- */
    private void OnFinalValidationResult(bool isCorrect)
    {
        if (isCorrect)
        {
            if (audioSource && successClip) audioSource.PlayOneShot(successClip);
            UIManager.Instance?.HideWordPopup();

            string msg = currentPuzzlePart?.victoryMessage;
            elevenLabsTTS?.Speak(string.IsNullOrEmpty(msg) ? "Bravo !!" : msg);
        }
        else
        {
            UIManager.Instance.ShowWordPopup(currentPuzzlePart.instruction);
            elevenLabsTTS?.Speak(currentPuzzlePart.audio_prompt);
            SpeechValidator.Instance.ListenForWord(currentPuzzlePart.word, OnFinalValidationResult);
        }
    }

    private void HandleWordValidated(PuzzleWord _)
    {
        if (audioSource && successClip) audioSource.PlayOneShot(successClip);
        UIManager.Instance?.HideWordPopup();
        StartCoroutine(ProceedToNextStepAfterDelay(1.5f));
    }

    /* ---------------------------------------------------------------- */
    /* Helpers                                                          */
    /* ---------------------------------------------------------------- */
    private IEnumerator RetrySpawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnNextPuzzlePart();
    }

    private IEnumerator ProceedToNextStepAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnNextPuzzlePart();
    }

    private Vector3 GetSpawnPosition()
    {
        if (!isSceneReady || MRUK.Instance.GetCurrentRoom() == null)
            return Camera.main.transform.position + Camera.main.transform.forward * 1.5f;

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        Vector3 pos;

        if (room.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.1f,
                new LabelFilter(MRUKAnchor.SceneLabels.TABLE), out pos, out _))
            return pos + Vector3.up * 0.02f;

        if (room.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.25f,
                new LabelFilter(MRUKAnchor.SceneLabels.FLOOR), out pos, out _))
            return pos + Vector3.up * 0.02f;

        return Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
    }
}
