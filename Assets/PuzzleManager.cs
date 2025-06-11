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
    public PuzzleGenerator puzzleGenerator; // Assign this in the Inspector

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip successClip;
    
    [Header("Puzzle Feedback UI")]
    public GameObject wordDisplayPrefab;
    public Transform memoryCorner;

    private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();
    private Queue<PuzzlePartData> puzzleQueue = new Queue<PuzzlePartData>();
    private PuzzlePartData currentPuzzlePart; 
    private List<GameObject> spawnedObjects = new List<GameObject>();
    
    private string currentNarrativeIntro;
    private bool isSceneReady = false;

    private void Awake() 
    { 
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        // --- NEW: Find PuzzleGenerator if not assigned in Inspector ---
        if (puzzleGenerator == null)
        {
            puzzleGenerator = FindObjectOfType<PuzzleGenerator>();
            if (puzzleGenerator == null)
            {
                Debug.LogError("PuzzleManager: PuzzleGenerator reference is missing and could not be found in scene. Please assign it in Inspector.");
            }
        }
    }

    private void Start() { StartCoroutine(RegisterSceneLoadedCallbackWhenReady()); }
    
    private IEnumerator RegisterSceneLoadedCallbackWhenReady()
    {
        while (MRUK.Instance == null) { yield return null; }
        MRUK.Instance.RegisterSceneLoadedCallback(() => {
            Debug.Log("MRUK Scene has loaded and is ready for spawning.");
            isSceneReady = true;
        });
    }

    /// <summary>
    /// Loads only the prefabs specified in the definitive list provided by PuzzleGenerator.
    /// This ensures consistency between what the AI can choose and what's actually loaded.
    /// </summary>
    public void LoadDefinitivePrefabs(HashSet<string> definitivePrefabPaths)
    {
        prefabDict.Clear(); // Clear any existing prefabs

        foreach (string fullResourcePath in definitivePrefabPaths)
        {
            // Extract the simple name from the full path (e.g., "moon" from "Prefabs/moon")
            string simpleName = fullResourcePath.Substring(fullResourcePath.LastIndexOf('/') + 1);

            // Load the actual GameObject prefab from Resources
            GameObject prefab = Resources.Load<GameObject>(prefabResourceFolder + "/" + simpleName);

            if (prefab != null)
            {
                if (!prefabDict.ContainsKey(fullResourcePath))
                {
                    prefabDict.Add(fullResourcePath, prefab);
                    Debug.Log($"Loaded prefab into dictionary: {fullResourcePath}"); 
                }
                else
                {
                    Debug.LogWarning($"Duplicate prefab name detected: {simpleName} at path {fullResourcePath}. Only the first instance will be used for dictionary lookup.");
                }
            }
            else
            {
                Debug.LogError($"Physical prefab '{simpleName}' (from path '{fullResourcePath}') listed in 'prefab_list.txt' was NOT found in Resources/{prefabResourceFolder}/. Please ensure physical prefab files match your list.");
            }
        }

        if (prefabDict.Count == 0)
        {
            Debug.LogError("No actual prefabs were loaded into PuzzleManager's dictionary. Check 'prefab_list.txt' and Resources/Prefabs folder.");
        }
    }


    /// <summary>
    /// Parses the JSON response from the LLM and populates the puzzle queue.
    /// </summary>
    /// <param name="json">The raw JSON string from the LLM.</param>
    public void CachePuzzlesFromResponse(string json)
    {
        puzzleQueue.Clear(); 
        try
        {
            JObject root = JObject.Parse(json);
            currentNarrativeIntro = root["narrative_intro"]?.ToString();
            JArray puzzlePartsArray = (JArray)root["puzzle_parts"]; 

            foreach (var part in puzzlePartsArray)
            {
                puzzleQueue.Enqueue(new PuzzlePartData {
                    word = part["word"]?.ToString(), 
                    prefab = part["prefab"]?.ToString(), 
                    instruction = part["instruction"]?.ToString(),
                    audio_prompt = part["audio_prompt"]?.ToString()
                });
            }

            string finalPromptText = root["final_prompt"]?.ToString();
            string expectedAnswerText = root["expected_answer"]?.ToString();
            string selectedPhrase = root["selected_phrase"]?.ToString();
            string victoryMessageText = root["victory_message"]?.ToString();
            
            // Enforce: expected_answer must match selected_phrase
            if (expectedAnswerText != selectedPhrase)
            {
                Debug.LogWarning($"'expected_answer' does not match 'selected_phrase'. Overriding: '{expectedAnswerText}' → '{selectedPhrase}'");
                expectedAnswerText = selectedPhrase;
            }

            if (!string.IsNullOrEmpty(finalPromptText) && !string.IsNullOrEmpty(expectedAnswerText))
            {
                puzzleQueue.Enqueue(new PuzzlePartData {
                    word = expectedAnswerText, 
                    prefab = "Prefabs/FinalPuzzle", 
                    instruction = finalPromptText,
                    audio_prompt = finalPromptText,
                    victoryMessage = victoryMessageText
                });
            }
        }
        catch (System.Exception ex) 
        { 
            Debug.LogError("Failed to parse puzzle JSON from LLM: " + ex.Message); 
            Debug.LogError("Raw JSON response: " + json);
        }
    }

    /// <summary>
    /// Initiates the puzzle sequence by playing the narrative intro and spawning the first puzzle part.
    /// </summary>
    public void StartPuzzleSequence()
    {
        // --- NEW: Load definitive prefabs if not already loaded ---
        if (prefabDict.Count == 0 && puzzleGenerator != null && puzzleGenerator.definitivePrefabNames.Count > 0)
        {
            LoadDefinitivePrefabs(puzzleGenerator.definitivePrefabNames);
        }
        
        if (elevenLabsTTS != null && !string.IsNullOrEmpty(currentNarrativeIntro))
        {
            elevenLabsTTS.Speak(currentNarrativeIntro);
        }
        else
        {
            Debug.LogWarning("ElevenlabsTTS is not assigned in Inspector, or narrative intro is empty. Cannot play narrative audio.");
        }
        SpawnNextPuzzlePart(); 
    }
    
    /// <summary>
    /// Spawns the next object for the current puzzle part and sets up its interaction.
    /// </summary>
    public void SpawnNextPuzzlePart() 
    {
        if (!isSceneReady) 
        { 
            Debug.LogWarning("MRUK scene not ready. Retrying spawn in 2 seconds."); 
            StartCoroutine(RetrySpawn(2f)); 
            return; 
        }
        
        if (puzzleQueue.Count == 0)
        {
            elevenLabsTTS.Speak("Félicitations ! Tu as tout terminé.");
            Debug.Log("All puzzle parts completed!");
            return;
        }

        foreach (GameObject obj in spawnedObjects) Destroy(obj);
        spawnedObjects.Clear();

        currentPuzzlePart = puzzleQueue.Dequeue(); 

        if (currentPuzzlePart.prefab == "Prefabs/FinalPuzzle")
        {
            Debug.Log("Initiating final puzzle validation for: " + currentPuzzlePart.word);
            UIManager.Instance.ShowWordPopup(currentPuzzlePart.instruction); 
            elevenLabsTTS.Speak(currentPuzzlePart.audio_prompt); 
            SpeechValidator.Instance.ListenForWord(currentPuzzlePart.word, OnFinalValidationResult);
            return; 
        }

        string prefabPathToSpawn = currentPuzzlePart.prefab; 

        if (prefabDict.ContainsKey(prefabPathToSpawn))
        {
            Vector3 spawnPosition = GetSpawnPosition();
            GameObject spawned = Instantiate(prefabDict[prefabPathToSpawn], spawnPosition, Quaternion.identity);
            spawnedObjects.Add(spawned);
            
            PuzzleWord puzzleWordComponent = spawned.GetComponent<PuzzleWord>();
            if (puzzleWordComponent != null)
            {
                puzzleWordComponent.Initialize(currentPuzzlePart.word, currentPuzzlePart.audio_prompt, null); 
                puzzleWordComponent.OnWordValidated += HandleWordValidated;
                Debug.Log($"Spawned: {prefabPathToSpawn} for word: {currentPuzzlePart.word}");
                
                // Auto-play instruction as narration
                if (!string.IsNullOrEmpty(currentPuzzlePart.instruction))
                {
                    Debug.Log($"[Instruction] {currentPuzzlePart.instruction}");
                    elevenLabsTTS.Speak(currentPuzzlePart.instruction);
                }
            }
            else
            {
                Debug.LogWarning($"Spawned prefab '{prefabPathToSpawn}' does not have a PuzzleWord component. Interaction will not work.");
            }
        }
        else 
        { 
            Debug.LogError($"CRITICAL: Missing prefab in dictionary for path: {prefabPathToSpawn}. This should have been caught by validation in PuzzleGenerator. Ensure 'prefab_list.txt' matches physical prefabs."); 
        }
    }

    private void OnFinalValidationResult(bool isCorrect)
    {
        if (isCorrect)
        {
            Debug.Log("Final phrase validated successfully! Game complete.");
            if (audioSource && successClip) audioSource.PlayOneShot(successClip);
            UIManager.Instance?.HideWordPopup();
            
            if (!string.IsNullOrEmpty(currentPuzzlePart?.victoryMessage))
                elevenLabsTTS.Speak(currentPuzzlePart.victoryMessage);
            else
                elevenLabsTTS.Speak("Bravo!!");

        }
        else
        {
            Debug.Log("Final phrase validation failed. Please try saying the complete phrase again.");
            UIManager.Instance.ShowWordPopup(currentPuzzlePart.instruction); 
            elevenLabsTTS.Speak(currentPuzzlePart.audio_prompt); 
            SpeechValidator.Instance.ListenForWord(currentPuzzlePart.word, OnFinalValidationResult); 
        }
    }

    private IEnumerator RetrySpawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnNextPuzzlePart();
    }
    
    private void HandleWordValidated(PuzzleWord validatedWord)
    {
        Debug.Log($"Word '{validatedWord.word}' validated for prefab.");
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
        SpawnNextPuzzlePart();
    }

    private Vector3 GetSpawnPosition()
    {
        if (!isSceneReady || MRUK.Instance.GetCurrentRoom() == null)
        {
            Debug.LogWarning("MRUK scene not ready during GetSpawnPosition. Spawning in front of camera as a fallback.");
            return Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
        }

        MRUKRoom currentRoom = MRUK.Instance.GetCurrentRoom();
        Vector3 position;
        
        var tableFilter = new LabelFilter(MRUKAnchor.SceneLabels.TABLE);
        if (currentRoom.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.1f, tableFilter, out position, out _))
        {
            Debug.Log("Found a valid spawn position on a TABLE.");
            return position + Vector3.up * 0.02f; 
        }

        var floorFilter = new LabelFilter(MRUKAnchor.SceneLabels.FLOOR);
        if (currentRoom.GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, 0.25f, floorFilter, out position, out _))
        {
            Debug.Log("No table found. Found a valid spawn position on the FLOOR.");
            return position + Vector3.up * 0.02f; 
        }
        
        Debug.LogWarning("No suitable TABLE or FLOOR surface found. Spawning in front of camera as ultimate fallback.");
        return Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
    }
}