using UnityEngine;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;               // for File.ReadAllText if you prefer non-Resources

public class PuzzleGenerator : MonoBehaviour
{
    // ---------------------------  Inspector  ---------------------------
    [Header("LLM Parameters")]
    [Tooltip("Target language, e.g. \"French\" or \"English\"")]
    public string playerLanguage = "French";

    [Tooltip("CEFR level, e.g. \"A1\", \"A2\", \"B1\", \"B2\"")]
    public string playerLevel = "A1";
    // -------------------------------------------------------------------

    private string groqApiKey;
    private const string groqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    public PuzzleManager puzzleManager;           // assign in Inspector

    // ---------- Internal request/response structures ----------
    private class GroqRequest  { public string model; public List<Message> messages; public int max_tokens; public ResponseFormat response_format; }
    private class ResponseFormat { public string type; }
    private class Message      { public string role;  public string content; }
    private class GroqResponse { public List<Choice> choices; }
    private class Choice       { public Message message; }
    // -----------------------------------------------------------

    public HashSet<string> definitivePrefabNames { get; private set; } = new HashSet<string>();

    // ---------------------------  Awake  ---------------------------
    void Awake()
    {
        // Load API key
        TextAsset keyFile = Resources.Load<TextAsset>("api_keys");
        if (keyFile != null)
        {
            var parsed = JsonUtility.FromJson<APIKeyContainer>(keyFile.text);
            groqApiKey = "Bearer " + parsed.groq;
        }
        else
        {
            Debug.LogError("api_keys.json not found in Resources!");
        }

        LoadDefinitivePrefabList();
    }
    [System.Serializable] public class APIKeyContainer { public string groq; }
    // ------------------------------------------------------------------

    private void LoadDefinitivePrefabList()
    {
        var loadedPrefabs = Resources.LoadAll<GameObject>("Prefabs");
        foreach (var p in loadedPrefabs)
            definitivePrefabNames.Add($"Prefabs/{p.name}");
    }

    // ---------------------------  PUBLIC entry  -----------------------
    public void StartPuzzleGeneration() => StartCoroutine(GeneratePuzzlesCoroutine());
    // ------------------------------------------------------------------

    // ---------------------------  Coroutine  ---------------------------
    private IEnumerator GeneratePuzzlesCoroutine()
    {
        if (definitivePrefabNames.Count == 0) { Debug.LogError("No prefabs!"); yield break; }

        // 1) ------ Build parameterised prompt ------
        //   a) partCount switch
        int partCount = playerLevel switch
        {
            "A1" => 2,
            "A2" => 3,
            "B1" => 4,
            _    => 5        // B2 or higher
        };

        //   b) Prefab list
        string prefabLine = "Prefabs: " + string.Join(", ", definitivePrefabNames);

        //   c) Load template
        TextAsset templateAsset = Resources.Load<TextAsset>("LLMPromptTemplate");   // make sure the txt file is in Resources
        if (templateAsset == null) { Debug.LogError("LLMPromptTemplate.txt not found in Resources"); yield break; }
        string promptTemplate = templateAsset.text;

        //   d) Replace placeholders
        string finalPrompt = promptTemplate
            .Replace("{language}",   playerLanguage)
            .Replace("{level}",      playerLevel)
            .Replace("{word_count}", partCount.ToString())
            .Replace("{0}",          prefabLine);

        // 2) ------ Prepare Groq request ------
        var requestData = new GroqRequest
        {
            model = "llama3-8b-8192",
            max_tokens = 500,
            response_format = new ResponseFormat { type = "json_object" },
            messages = new List<Message> { new Message { role = "user", content = finalPrompt } }
        };

        // 3) ------ Send request ------
        byte[] body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestData));
        using UnityWebRequest req = new UnityWebRequest(groqEndpoint, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", groqApiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Groq error: {req.error}\n{req.downloadHandler.text}");
            yield break;
        }

        // 4) ------ Parse & validate ------
        var groqResp = JsonConvert.DeserializeObject<GroqResponse>(req.downloadHandler.text);
        string puzzleJson = groqResp?.choices?[0]?.message?.content;
        Debug.Log("[LLM Raw Puzzle JSON] " + puzzleJson);

        if (ValidatePuzzleJson(puzzleJson, definitivePrefabNames))
        {
            puzzleManager.CachePuzzlesFromResponse(puzzleJson);
            puzzleManager.StartPuzzleSequence();
        }
    }
    // ------------------------------------------------------------------

    private bool ValidatePuzzleJson(string json, HashSet<string> valid)
    {
        if (string.IsNullOrEmpty(json)) return false;
        try
        {
            JObject root = JObject.Parse(json);
            foreach (var part in root["puzzle_parts"])
            {
                string path = part["prefab"]?.ToString();
                if (path == "Prefabs/FinalPuzzle") continue;        // always allowed
                if (!valid.Contains(path))
                {
                    Debug.LogError($"Invalid prefab in JSON: {path}");
                    return false;
                }
            }
            return true;
        }
        catch { return false; }
    }
}
