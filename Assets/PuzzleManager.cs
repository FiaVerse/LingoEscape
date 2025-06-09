using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using Newtonsoft.Json.Linq;
using TMPro;

public class PuzzleManager : MonoBehaviour
{
    [System.Serializable]
    public class Puzzle
    {
        public string instruction;
        public string audio_prompt;
        public string[] objects;
        public string answer;
    }

    [Header("Game Configuration")]
    public string prefabResourceFolder = "Prefabs";
   // public GroqTTS groqTTS;
    public ElevenlabsTTS elevenLabsTTS;
    
    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip successClip;
    
    [Header("Puzzle Feedback UI")]
    public GameObject wordDisplayPrefab;
    public Transform memoryCorner;

    private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();
    private Queue<Puzzle> puzzleQueue = new Queue<Puzzle>();
    private Puzzle currentPuzzle;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    
    // We no longer need to cache anchors ourselves, MRUK does it.
    // private List<MRUKAnchor> cachedAnchors = new List<MRUKAnchor>();

    private string currentNarrativeIntro;
    private bool isSceneReady = false;

    private void Awake() { LoadAllPrefabs(); }
    private void Start() { StartCoroutine(RegisterSceneLoadedCallbackWhenReady()); }
    
    private IEnumerator RegisterSceneLoadedCallbackWhenReady()
    {
        while (MRUK.Instance == null) { yield return null; }
        MRUK.Instance.RegisterSceneLoadedCallback(() => {
            Debug.Log("MRUK Scene has loaded and is ready.");
            isSceneReady = true;
        });
    }

    private void LoadAllPrefabs()
    {
        GameObject[] prefabs = Resources.LoadAll<GameObject>(prefabResourceFolder);
        foreach (GameObject prefab in prefabs)
        {
            string key = prefab.name.ToLower().Replace(" ", "_");
            if (!prefabDict.ContainsKey(key)) { prefabDict.Add(key, prefab); }
        }
    }

    public void CachePuzzlesFromResponse(string json)
    {
        puzzleQueue.Clear();
        try
        {
            JObject root = JObject.Parse(json);
            currentNarrativeIntro = root["narrative_intro"]?.ToString();
            JArray parts = (JArray)root["puzzle_parts"];
            foreach (var part in parts)
            {
                puzzleQueue.Enqueue(new Puzzle {
                    instruction = part["instruction"].ToString(),
                    audio_prompt = part["audio_prompt"].ToString(),
                    objects = new string[] { part["prefab"].ToString() },
                    answer = part["word"].ToString()
                });
            }
            if (!string.IsNullOrEmpty(root["expected_answer"]?.ToString()))
            {
                puzzleQueue.Enqueue(new Puzzle {
                    instruction = root["final_prompt"]?.ToString(),
                    audio_prompt = root["final_prompt"]?.ToString(),
                    objects = new string[] { "FinalPuzzleActivator" },
                    answer = root["expected_answer"].ToString()
                });
            }
        }
        catch (System.Exception ex) { Debug.LogError("Failed to parse puzzle JSON: " + ex.Message); }
    }

    public void StartPuzzleSequence()
    {
        //if (groqTTS != null && !string.IsNullOrEmpty(currentNarrativeIntro))
       // {
        //   groqTTS.Speak(currentNarrativeIntro);
       // }
       
        elevenLabsTTS.Speak(currentNarrativeIntro);
        SpawnNextPuzzle();
    }

    public void SpawnNextPuzzle()
    {
        if (!isSceneReady) { Debug.LogWarning("Scene not ready. Will retry in 2 seconds."); StartCoroutine(RetrySpawn(2f)); return; }
        if (puzzleQueue.Count == 0)
        {
            //if(groqTTS != null) groqTTS.Speak("Félicitations ! Tu as tout terminé.");
            elevenLabsTTS.Speak("Félicitations !");
            return;
        }

        foreach (GameObject obj in spawnedObjects) Destroy(obj);
        spawnedObjects.Clear();

        currentPuzzle = puzzleQueue.Dequeue();
        
        foreach (string obj in currentPuzzle.objects)
        {
            string key = obj.Trim().ToLower().Replace(" ", "_");
            if (prefabDict.ContainsKey(key))
            {
                // Get the spawn position using our new and improved method.
                Vector3 spawnPosition = GetSpawnPosition();

                GameObject spawned = Instantiate(prefabDict[key], spawnPosition, Quaternion.identity);
                spawnedObjects.Add(spawned);
                
                PuzzleWord puzzleWordComponent = spawned.GetComponent<PuzzleWord>();
                if (puzzleWordComponent != null)
                {
                    puzzleWordComponent.Initialize(currentPuzzle.answer, currentPuzzle.audio_prompt, null);
                    puzzleWordComponent.OnWordValidated += HandleWordValidated;
                }
            }
            else { Debug.LogWarning("Missing prefab: " + obj); }
        }
    }

    private IEnumerator RetrySpawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnNextPuzzle();
    }
    
    private void HandleWordValidated(PuzzleWord validatedWord)
    {
        if (audioSource && successClip) audioSource.PlayOneShot(successClip);
        if (UIManager.Instance?.wordPopupText != null) UIManager.Instance.wordPopupText.color = Color.green;
        
        if (wordDisplayPrefab != null && memoryCorner != null)
        {
            GameObject wordCard = Instantiate(wordDisplayPrefab, memoryCorner);
            TMP_Text textComponent = wordCard.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = validatedWord.word;
                textComponent.color = Color.green;
            }
        }
        StartCoroutine(ProceedToNextStepAfterDelay(1.5f));
    }
    
    private IEnumerator ProceedToNextStepAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        UIManager.Instance?.HideWordPopup();
        SpawnNextPuzzle();
    }

    /// <summary>
    /// Finds a valid spawn position in the room using MRUK's official API.
    /// It prioritizes tables, then floors, and has a fallback if no surfaces are found.
    /// </summary>
    /// <returns>A valid world-space position vector for spawning an object.</returns>
    private Vector3 GetSpawnPosition()
    {
        // Ensure the scene is loaded and we can get the current room.
        if (!isSceneReady || MRUK.Instance.GetCurrentRoom() == null)
        {
            Debug.LogWarning("MRUK scene not ready. Spawning in front of camera as a fallback.");
            return Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
        }

        MRUKRoom currentRoom = MRUK.Instance.GetCurrentRoom();
        Vector3 position;
        
        // --- Attempt 1: Find a random spot on a TABLE ---
        // We define a filter to only look for anchors with the TABLE label.
        var tableFilter = new LabelFilter(MRUKAnchor.SceneLabels.TABLE);
        // We ask for a position on any upward-facing surface that matches our filter.
        // The 0.1f means the position must be at least 10cm from any edge.
        if (currentRoom.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.1f, tableFilter, out position, out _))
        {
            Debug.Log("Found a valid spawn position on a TABLE.");
            // We add a small upward offset to ensure the object spawns on top of, not inside, the surface.
            return position + Vector3.up * 0.02f;
        }

        // --- Attempt 2: If no table, find a random spot on the FLOOR ---
        var floorFilter = new LabelFilter(MRUKAnchor.SceneLabels.FLOOR);
        // We use a larger edge distance for the floor to avoid spawning too close to walls.
        if (currentRoom.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.25f, floorFilter, out position, out _))
        {
            Debug.Log("No table found. Found a valid spawn position on the FLOOR.");
            return position + Vector3.up * 0.02f;
        }
        
        // --- Attempt 3: Fallback if no suitable surface is found ---
        Debug.LogWarning("No suitable TABLE or FLOOR surface found. Spawning in front of camera.");
        return Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
    }
}