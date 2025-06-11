using UnityEngine;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class PuzzleGenerator : MonoBehaviour
{
    private string groqApiKey;
    private string groqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    public PuzzleManager puzzleManager; // Assign this in the Inspector

    // --- Data Structures (unchanged) ---
    private class GroqRequest { public string model; public List<Message> messages; public int max_tokens; public ResponseFormat response_format; }
    private class ResponseFormat { public string type; } 
    private class Message { public string role; public string content; }
    private class GroqResponse { public List<Choice> choices; }
    private class Choice { public Message message; }

    // --- NEW: Public property for definitive prefab names ---
    public HashSet<string> definitivePrefabNames { get; private set; } = new HashSet<string>();

    void Awake()
    {
        TextAsset keyFile = Resources.Load<TextAsset>("api_keys");
        if (keyFile != null)
        {
            var parsed = JsonUtility.FromJson<APIKeyContainer>(keyFile.text);
            groqApiKey = "Bearer " + parsed.groq;
        }
        else
        {
            Debug.LogError("API Key file 'api_keys.json' not found in Resources folder. Please create it with a 'groq' field.");
        }
        
        LoadDefinitivePrefabList(); // Load your list from the text file
    }

    [System.Serializable]
    public class APIKeyContainer { public string groq; }

    private void LoadDefinitivePrefabList()
    {
        GameObject[] loadedPrefabs = Resources.LoadAll<GameObject>("Prefabs");

        if (loadedPrefabs.Length == 0)
        {
            Debug.LogError("No prefabs found in Resources/Prefabs. Make sure your prefabs are in the right folder.");
            return;
        }

        foreach (GameObject prefab in loadedPrefabs)
        {
            string fullPath = $"Prefabs/{prefab.name}";
            definitivePrefabNames.Add(fullPath);
            Debug.Log($"Loaded prefab from Resources: {fullPath}");
        }
    }



    public void StartPuzzleGeneration()
    {
        StartCoroutine(GeneratePuzzlesCoroutine());
    }

    private IEnumerator GeneratePuzzlesCoroutine()
    {
        Debug.Log("GeneratePuzzles Coroutine started.");

        if (puzzleManager == null) { Debug.LogError("PuzzleGenerator: PuzzleManager is not assigned!"); yield break; }
        if (string.IsNullOrEmpty(groqApiKey) || groqApiKey == "Bearer ") { Debug.LogError("PuzzleGenerator: API Key is missing or invalid!"); yield break; }
        if (definitivePrefabNames.Count == 0) { Debug.LogError("PuzzleGenerator: Definitive prefab list is empty. Cannot generate puzzles."); yield break; }


        // --- Load prompt template ---
        TextAsset promptTemplateAsset = Resources.Load<TextAsset>("LLMPromptTemplate"); 
        if (promptTemplateAsset == null)
        {
            Debug.LogError("File Error: Could not load 'LLMPromptTemplate.txt' from Resources folder. Please ensure it exists.");
            yield break;
        }
//
      
        // --- Build the flat prefab list string for the LLM prompt ---
        string prefabsForLLMPrompt = "Prefabs: " + string.Join(", ", definitivePrefabNames);

        
        //
        
        Debug.Log("Prefabs list generated for LLM from definitive list: " + prefabsForLLMPrompt); 

        // --- Format the final prompt with the dynamically generated prefabs JSON ---
        string promptTemplate = promptTemplateAsset.text;
        string finalPrompt = promptTemplate.Replace("{0}", prefabsForLLMPrompt);

        
        Debug.Log("Prompt prepared. Building web request...");

        // --- Building the web request using UnityWebRequest ---
        var requestData = new GroqRequest
        {
            model = "llama3-8b-8192", 
            messages = new List<Message> { new Message { role = "user", content = finalPrompt } },
            max_tokens = 500, 
            response_format = new ResponseFormat { type = "json_object" }
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(groqEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", groqApiKey);

            Debug.Log("Making API call to Groq...");
            yield return request.SendWebRequest(); 

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API call failed: {request.error} - Response: {request.downloadHandler.text}");
            }
            else
            {
                Debug.Log("API call successful. Parsing response...");
                string result = request.downloadHandler.text;
                try
                {
                    var groqResponse = JsonConvert.DeserializeObject<GroqResponse>(result);
                    string puzzleJson = groqResponse?.choices?[0]?.message?.content;
                    Debug.Log("[LLM Raw Puzzle JSON] " + puzzleJson);

                    if (!string.IsNullOrEmpty(puzzleJson))
                    {
                        Debug.Log("Successfully received puzzle JSON from LLM. Validating prefab paths...");
                        // --- Validate the AI's chosen prefabs against the definitive list ---
                        if (ValidatePuzzleJson(puzzleJson, definitivePrefabNames))
                        {
                             puzzleManager.CachePuzzlesFromResponse(puzzleJson);
                             puzzleManager.StartPuzzleSequence();
                        }
                        else
                        {
                            Debug.LogError("AI generated invalid prefab paths. Puzzle cannot be generated. Please adjust prompt or prefab list.");
                            // We don't call puzzleManager.CachePuzzlesFromResponse here
                            // This means the game won't proceed with a broken puzzle
                        }
                    }
                    else
                    {
                        Debug.LogError("API returned an empty or null content field in the response.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to parse JSON response from LLM: {e.Message}. Raw response: {result}");
                }
            }
        }
    }

    /// <summary>
    /// Validates that all prefab paths chosen by the LLM in the generated JSON are in the definitive list.
    /// It does NOT correct them; it only checks.
    /// </summary>
    /// <param name="puzzleJson">The raw JSON string from the LLM.</param>
    /// <param name="validPaths">A HashSet of all definitive valid prefab paths (e.g., "Prefabs/moon").</param>
    /// <returns>True if all chosen prefabs are valid, false otherwise.</returns>
    private bool ValidatePuzzleJson(string puzzleJson, HashSet<string> validPaths)
    {
        try
        {
            JObject puzzleData = JObject.Parse(puzzleJson);
            JArray puzzleParts = (JArray)puzzleData["puzzle_parts"];
            bool allValid = true;

            foreach (JObject part in puzzleParts)
            {
                string chosenPrefabPath = part["prefab"]?.ToString();
                
                // If it's the special FinalPuzzleActivator, it's always valid
                if (chosenPrefabPath == "FinalPuzzleActivator") continue;

                if (!validPaths.Contains(chosenPrefabPath))
                {
                    Debug.LogError($"Validation failed: LLM chose invalid prefab path: '{chosenPrefabPath}'. This prefab is not in your 'prefab_list.txt'.");
                    allValid = false;
                }
            }
            return allValid;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error validating LLM puzzle JSON: {e.Message}. Invalid JSON structure?");
            return false; 
        }
    }
}