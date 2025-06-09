using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ElevenlabsTTS : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    private const string apiUrl = "https://api.elevenlabs.io/v1/text-to-speech/";
    [SerializeField] private string voiceId = "your_voice_id_here"; 
    private string apiKey;

    public static ElevenlabsTTS Instance { get; private set; }
    void Awake()
    {
        TextAsset keyFile = Resources.Load<TextAsset>("api_keys");
        if (keyFile != null)
        {
            var parsed = JsonUtility.FromJson<APIKeyContainer>(keyFile.text);
            apiKey = parsed.elevenlabs;
        }
        else
        {
            Debug.LogError("api_keys.json not found in Resources folder.");
        }
    }

    [System.Serializable]
    public class APIKeyContainer
    {
        public string groq;
        public string elevenlabs;
    }

    public async void Speak(string text)
    {
        await GenerateAndPlaySpeech(text);
    }

    public async Task GenerateAndPlaySpeech(string text)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        var json = $"{{\"text\":\"{text}\",\"model_id\":\"eleven_turbo_v2_5\",\"voice_settings\":{{\"stability\":0.5,\"similarity_boost\":0.75}}}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(apiUrl + voiceId + "?output_format=mp3_44100_128", content);
            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"TTS API call failed: {response.StatusCode}");
                Debug.LogError(await response.Content.ReadAsStringAsync());
                return;
            }

            byte[] mp3Data = await response.Content.ReadAsByteArrayAsync();
            string tempPath = Path.Combine(Application.persistentDataPath, "tempTTS.mp3");
            File.WriteAllBytes(tempPath, mp3Data);

            using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG);
            var asyncOp = www.SendWebRequest();

            while (!asyncOp.isDone)
                await Task.Yield();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load audio clip: " + www.error);
                return;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.clip = clip;
            audioSource.Play();
        }
        catch (Exception ex)
        {
            Debug.LogError("Error generating TTS: " + ex.Message);
        }
    }
}
