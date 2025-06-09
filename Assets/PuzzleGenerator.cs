using UnityEngine;
using System.Collections; // NEW: Needed for Coroutines
using System.Text;
using System.Collections.Generic;
using UnityEngine.Networking; // NEW: Using Unity's native web request system
using Newtonsoft.Json;

public class PuzzleGenerator : MonoBehaviour
{
    private string groqApiKey;
    private string groqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    public PuzzleManager puzzleManager;

    // --- Data Structures remain the same ---
    private class GroqRequest { public string model; public List<Message> messages; public int max_tokens; public ResponseFormat response_format; }
    private class ResponseFormat { public string type; }
    private class Message { public string role; public string content; }
    private class GroqResponse { public List<Choice> choices; }
    private class Choice { public Message message; }

    void Awake()
    {
        TextAsset keyFile = Resources.Load<TextAsset>("api_keys");
        if (keyFile != null)
        {
            var parsed = JsonUtility.FromJson<APIKeyContainer>(keyFile.text);
            groqApiKey = "Bearer " + parsed.groq;
        }
    }

    [System.Serializable]
    public class APIKeyContainer { public string groq; }

    public void StartPuzzleGeneration()
    {
        // This now starts a Coroutine instead of an async Task.
        StartCoroutine(GeneratePuzzlesCoroutine());
    }

    // This method is now a Coroutine, which is more stable in Unity.
    private IEnumerator GeneratePuzzlesCoroutine()
    {
        Debug.Log("GeneratePuzzles Coroutine started.");

        if (puzzleManager == null) { Debug.LogError("PuzzleGenerator: PuzzleManager is not assigned!"); yield break; }
        if (string.IsNullOrEmpty(groqApiKey) || groqApiKey == "Bearer ") { Debug.LogError("PuzzleGenerator: API Key is missing!"); yield break; }

        // --- Loading data remains the same ---
        TextAsset promptTemplateAsset = Resources.Load<TextAsset>("prompt_puzzle_generator");
        TextAsset prefabAsset = Resources.Load<TextAsset>("prefab_list");
        TextAsset phraseAsset = Resources.Load<TextAsset>("phrases_list");

        if (promptTemplateAsset == null || prefabAsset == null || phraseAsset == null)
        {
            Debug.LogError("File Error: Could not load required text files from Resources folder.");
            yield break;
        }

        List<string> prefabList = new List<string>(prefabAsset.text.Split('\n'));
        List<string> a1Phrases = new List<string>();
        string currentLevel = "";
        foreach (string line in phraseAsset.text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#")) currentLevel = trimmed.Substring(1).Trim();
            else if (!string.IsNullOrWhiteSpace(trimmed) && currentLevel == "A1") a1Phrases.Add(trimmed);
        }

        string promptTemplate = promptTemplateAsset.text;
        string finalPrompt = string.Format(promptTemplate, string.Join(", ", a1Phrases), string.Join(", ", prefabList));
        
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
                Debug.LogError($"API call failed: {request.error} - {request.downloadHandler.text}");
            }
            else
            {
                Debug.Log("API call successful. Parsing response...");
                string result = request.downloadHandler.text;
                try
                {
                    var groqResponse = JsonConvert.DeserializeObject<GroqResponse>(result);
                    string puzzleJson = groqResponse?.choices?[0]?.message?.content;

                    if (!string.IsNullOrEmpty(puzzleJson))
                    {
                        Debug.Log("Successfully received puzzle JSON. Caching and starting sequence.");
                        puzzleManager.CachePuzzlesFromResponse(puzzleJson);
                        puzzleManager.StartPuzzleSequence();
                    }
                    else
                    {
                        Debug.LogError("API returned a response but the content was empty.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to parse JSON response: {e.Message}");
                }
            }
        }
    }
}