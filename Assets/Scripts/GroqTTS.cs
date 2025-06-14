using UnityEngine;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class GroqTTS : MonoBehaviour
{
    public AudioSource audioSource;
    [SerializeField] private string apiKey = "your_groq_api_key"; // Replace this
    private const string apiUrl = "https://api.groq.com/openai/v1/audio/speech";
    private const string model = "playai-tts";
    private const string voice = "Cheyenne-PlayAI";
    private const string format = "wav";  // Switch from "pcm" to "wav"

    public static GroqTTS Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    
    public async void Speak(string text)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var json = $"{{\"model\":\"{model}\",\"voice\":\"{voice}\",\"input\":\"{EscapeJson(text)}\",\"response_format\":\"{format}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(apiUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError("Groq TTS failed: " + response.StatusCode);
                Debug.LogError(await response.Content.ReadAsStringAsync());
                return;
            }

            byte[] wav = await response.Content.ReadAsByteArrayAsync();
            AudioClip clip = CreateClipFromWav(wav);
            audioSource.clip = clip;
            audioSource.Play();
        }
        catch (Exception ex)
        {
            Debug.LogError("Groq TTS error: " + ex.Message);
        }
    }

    private string EscapeJson(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private AudioClip CreateClipFromWav(byte[] wav)
    {
        int channels = BitConverter.ToInt16(wav, 22);
        int sampleRate = BitConverter.ToInt32(wav, 24);
        int bitsPerSample = BitConverter.ToInt16(wav, 34);

        int dataStart = FindDataChunk(wav) + 8;
        int sampleCount = (wav.Length - dataStart) / (bitsPerSample / 8);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            int sampleIndex = dataStart + i * 2; // assuming 16-bit PCM
            short sample = BitConverter.ToInt16(wav, sampleIndex);
            samples[i] = sample / 32768f;
        }

        AudioClip clip = AudioClip.Create("GroqTTS_Audio", sampleCount / channels, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private int FindDataChunk(byte[] wav)
    {
        for (int i = 12; i < wav.Length - 4; i++)
        {
            if (wav[i] == 'd' && wav[i + 1] == 'a' && wav[i + 2] == 't' && wav[i + 3] == 'a')
            {
                return i;
            }
        }
        throw new Exception("DATA chunk not found in WAV");
    }
}
