using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// An all-in-one component for recording audio from the microphone,
/// saving it as a .wav file, and transcribing it using the Whisper API.
/// </summary>
public class WhisperSTT : MonoBehaviour
{
    [Header("API Configuration")]
    [Tooltip("The API endpoint for the transcription service.")]
    public string whisperEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
    private string apiKey;

    [Header("Recording Settings")]
    [Tooltip("Maximum recording duration in seconds.")]
    public int recordingDuration = 5;

    private bool isRecording = false;
    private string tempWavFilePath;

    // --- Data structures for API calls ---
    private class TranscriptionResponse { public string text; }
    [System.Serializable] private class APIKeyContainer { public string groq; }

    /// <summary>
    /// Called once when the script instance is being loaded.
    /// We use it to securely load the API key and set up file paths.
    /// </summary>
    private void Awake()
    {
        LoadAPIKey();
        // Define a consistent path for our temporary recording file.
        tempWavFilePath = Path.Combine(Application.persistentDataPath, "temp_recording.wav");
    }

    private void LoadAPIKey()
    {
        TextAsset keyFile = Resources.Load<TextAsset>("api_keys");
        if (keyFile != null)
        {
            APIKeyContainer keys = JsonUtility.FromJson<APIKeyContainer>(keyFile.text);
            apiKey = keys.groq;
        }
        else
        {
            Debug.LogError("WhisperSTT: 'api_keys.json' could not be found in the Resources folder!");
        }
    }

    /// <summary>
    /// The main public method to start the entire process.
    /// It records audio for a set duration, then starts transcription.
    /// </summary>
    /// <param name="onTranscriptionComplete">The callback action to invoke with the resulting text.</param>
    public void StartRecordingAndTranscription(System.Action<string> onTranscriptionComplete)
    {
        if (isRecording)
        {
            Debug.LogWarning("WhisperSTT: Cannot start a new recording while one is already in progress.");
            return;
        }
        StartCoroutine(RecordingAndTranscriptionCoroutine(onTranscriptionComplete));
    }

    private IEnumerator RecordingAndTranscriptionCoroutine(System.Action<string> onTranscriptionComplete)
    {
        isRecording = true;
        Debug.Log("WhisperSTT: Starting recording...");

        // Start recording from the default microphone.
        AudioClip recordingClip = Microphone.Start(null, false, recordingDuration, 44100);
        yield return new WaitForSeconds(recordingDuration);
        Microphone.End(null);

        Debug.Log("WhisperSTT: Recording finished. Saving to .wav file using SavWav...");

        // Use the SavWav utility to save the AudioClip to our temporary file path.
        // The 'true' returned by Save isn't used here, but the function creates the file.
        SavWav.Save(tempWavFilePath, recordingClip);
        
        Debug.Log("WhisperSTT: .wav file saved at: " + tempWavFilePath);
        
        // Start the transcription process using the saved file.
        yield return TranscribeCoroutine(tempWavFilePath, onTranscriptionComplete);

        isRecording = false;
    }

    private IEnumerator TranscribeCoroutine(string filePath, System.Action<string> onResult)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("WhisperSTT: API Key is not loaded. Aborting transcription.");
            onResult?.Invoke(null);
            yield break;
        }

        byte[] audioData = File.ReadAllBytes(filePath);
        WWWForm form = new WWWForm();
        form.AddField("model", "whisper-large-v3");
        form.AddBinaryData("file", audioData, Path.GetFileName(filePath), "audio/wav");

        using (UnityWebRequest request = UnityWebRequest.Post(whisperEndpoint, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("WhisperSTT Transcription failed: " + request.error + " - " + request.downloadHandler.text);
                onResult?.Invoke(null);
            }
            else
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<TranscriptionResponse>(request.downloadHandler.text);
                    onResult?.Invoke(response.text);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("WhisperSTT: Failed to parse transcription JSON: " + ex.Message);
                    onResult?.Invoke(null);
                }
            }
        }
    }
}