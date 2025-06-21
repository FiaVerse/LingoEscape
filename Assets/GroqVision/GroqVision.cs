/*
 * GroqVisionPassthrough.cs
 * Unity 2022+ / Quest-ready
 * Reads a single frame from Meta’s passthrough camera and sends it to Groq Vision.
 * Requires:  - WebCamTextureManagerPrefab in scene
 *            - Newtonsoft.Json (com.unity.nuget.newtonsoft-json)
 */

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using PassthroughCameraSamples;   // ← namespace of WebCamTextureManager

public class GroqVisionPassthrough : MonoBehaviour
{
    /* ───────────── Inspector ───────────── */

    [Header("Groq API")]
    [Tooltip("Paste your Groq Cloud API key")]
    public string groqApiKey = "YOUR_GROQ_API_KEY_HERE";

    [Header("Model")]
    public GroqVisionModel selectedModel = GroqVisionModel.Scout;

    [Header("Language")]
    public string targetLanguage = "French";

    [Header("Image Processing")]
    public int maxImageResolution = 1280;
    [Range(50, 100)] public int compressionQuality = 85;

    [Header("TTS")]
    public GroqTTS ttsPlayer;

    [Header("Passthrough Camera")]
    [Tooltip("Leave empty → auto-find")]
    public WebCamTextureManager camMgr;

    [Header("Debug")]
    public bool enableDebugLogging = true;

    public enum GroqVisionModel { Scout, Maverick }

    /* ───────────── State ───────────── */

    bool      isProcessing;
    Coroutine currentRequest;

    /* ───────────── Unity ───────────── */

    void Awake()
    {
        if (!camMgr) camMgr = FindObjectOfType<WebCamTextureManager>();
        if (!camMgr) { LogError("WebCamTextureManager not found"); enabled = false; }
    }

    void Start()      => ValidateConfiguration();
    void OnDestroy()  => CancelCurrentRequest();

    /* ───────────── Public API ───────────── */

    public bool   IsProcessing => isProcessing;
    public string CurrentModel => GetSelectedModelName();

    public void AnalyzeAndSpeak()
    {
        if (isProcessing)            { Log("⏳ Already working"); return; }
        if (!enabled)                { LogError("Component disabled"); return; }

        currentRequest = StartCoroutine(CaptureAndAnalyzeImage());
    }

    public void CancelCurrentRequest()
    {
        if (currentRequest == null) return;
        StopCoroutine(currentRequest);
        currentRequest = null;
        isProcessing   = false;
        Log("🚫 Cancelled");
    }

    /* ───────────── Main coroutine ───────────── */

    IEnumerator CaptureAndAnalyzeImage()
    {
        isProcessing = true;

        // ensure cam is running
        if (!camMgr.WebCamTexture || !camMgr.WebCamTexture.isPlaying)
        {
            LogError("Passthrough camera not ready");
            isProcessing = false;
            yield break;
        }

        // block for current frame
        yield return new WaitForEndOfFrame();

        if (!TryMakeBase64(out string base64Img))
        {
            isProcessing = false;
            yield break;
        }

        yield return SendToGroqAPI(base64Img);

        isProcessing  = false;
        currentRequest = null;
    }

    /* ───────────── Synchronous helpers ───────────── */

    bool TryMakeBase64(out string base64)
    {
        base64 = null;
        Texture2D snap = null;

        try
        {
            snap = CapturePassthroughFrame();
            if (!snap) return false;

            Texture2D processed = ProcessImage(snap);
            byte[] jpg = processed.EncodeToJPG(compressionQuality);
            if (jpg == null || jpg.Length == 0) { LogError("Encoding failed"); return false; }

            float mb = jpg.Length / (1024f * 1024f);
            Log($"📏 Size: {mb:F2} MB");
            if (mb > 3.8f) { LogError("Image >4 MB limit"); return false; }

            base64 = Convert.ToBase64String(jpg);
            if (processed != snap) Destroy(processed);
            return true;
        }
        catch (Exception e) { LogError($"Processing error: {e.Message}"); return false; }
        finally
        {
            if (snap) Destroy(snap);
        }
    }

    Texture2D CapturePassthroughFrame()
    {
        var wct = camMgr.WebCamTexture;
        if (wct.width == 0 || wct.height == 0) { LogError("Invalid cam texture"); return null; }

        Texture2D tex = new Texture2D(wct.width, wct.height, TextureFormat.RGB24, false);
        tex.SetPixels(wct.GetPixels());   // blocking, simple
        tex.Apply();
        return tex;
    }

    Texture2D ProcessImage(Texture2D src)
    {
        int w = src.width, h = src.height, max = Math.Max(w, h);
        if (max <= maxImageResolution) return src;

        float scale = (float)maxImageResolution / max;
        int nw = Mathf.RoundToInt(w * scale), nh = Mathf.RoundToInt(h * scale);
        Log($"🔄 Resize {w}×{h} → {nw}×{nh}");

        var dst = new Texture2D(nw, nh, TextureFormat.RGB24, false);
        for (int y = 0; y < nh; y++)
        for (int x = 0; x < nw; x++)
            dst.SetPixel(x, y, src.GetPixelBilinear((float)x / nw, (float)y / nh));
        dst.Apply();
        return dst;
    }

    /* ───────────── Networking (unchanged) ───────────── */

    IEnumerator SendToGroqAPI(string base64Image)
    {
        const string url = "https://api.groq.com/openai/v1/chat/completions";
        string modelId   = GetSelectedModelName();

        string prompt =
            "What is the name of the object in this image in french? " +
            "answer with the just the name in french and nothing else.";

        var payload = new
        {
            messages = new[] {
                new {
                    role = "user",
                    content = new object[] {
                        new { type = "text",      text = prompt },
                        new { type = "image_url", image_url = new { url = "data:image/jpeg;base64," + base64Image } }
                    }
                }
            },
            model                 = modelId,
            temperature           = 1,
            max_completion_tokens = 1024,
            top_p                 = 1,
            stream                = false,
            stop                  = (string?)null
        };

        string json = JsonConvert.SerializeObject(payload, Formatting.None,
                         new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        using var req = new UnityWebRequest(url, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout         = 30
        };
        req.SetRequestHeader("Content-Type",  "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {groqApiKey}");

        Log("📤 Posting to Groq…");
        yield return req.SendWebRequest();
        yield return ProcessAPIResponse(req);
    }

    IEnumerator ProcessAPIResponse(UnityWebRequest req)
    {
        if (req.result == UnityWebRequest.Result.Success)
        {
            string raw = req.downloadHandler.text;
            string word = ExtractTranslationFromResponse(raw);
            if (string.IsNullOrEmpty(word)) { LogWarning("No answer found"); }
            else
            {
                word = CleanTranslationText(word);
                Log($"🌍 '{word}'");
                if (ttsPlayer) ttsPlayer.Speak(word);
            }
        }
        else LogError($"❌ {req.responseCode}\n{req.downloadHandler.text}");

        yield return null;
    }

    string ExtractTranslationFromResponse(string json)
    {
        try
        {
            var wrapper = JsonConvert.DeserializeObject<GroqResponseWrapper>(json);
            return wrapper?.choices?[0]?.message?.content?.Trim();
        }
        catch (Exception e) { LogError($"Parse error: {e.Message}"); return null; }
    }

    string CleanTranslationText(string txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return txt;
        txt = txt.Trim().Trim('"').Replace(".", "").Replace(",", "");
        var parts = txt.Split(' ');
        if (parts.Length > 3) txt = string.Join(" ", parts, 0, 3);
        return txt;
    }

    /* ───────────── Misc ───────────── */

    string GetSelectedModelName() =>
        selectedModel == GroqVisionModel.Maverick
            ? "meta-llama/llama-4-maverick-17b-128e-instruct"
            : "meta-llama/llama-4-scout-17b-16e-instruct";

    void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(groqApiKey) || groqApiKey == "YOUR_GROQ_API_KEY_HERE")
        { LogError("API key missing"); enabled = false; }

        if (string.IsNullOrEmpty(targetLanguage))
        { LogWarning("Language defaulted to French"); targetLanguage = "French"; }

        Log("✅ Config OK");
    }

    /* ───────────── Logging ───────────── */

    void Log(string m)        { if (enableDebugLogging) Debug.Log($"[GroqVision] {m}"); }
    void LogWarning(string m) { Debug.LogWarning($"[GroqVision] ⚠️ {m}"); }
    void LogError(string m)   { Debug.LogError($"[GroqVision] ❌ {m}"); }

    /* ───────────── DTOs ───────────── */

    [Serializable] public class GroqResponseWrapper { public Choice[] choices; }
    [Serializable] public class Choice             { public GroqMessage message; }
    [Serializable] public class GroqMessage        { public string content; }
}
