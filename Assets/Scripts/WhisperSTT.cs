using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.Networking;
using Newtonsoft.Json;
using TMPro;

public class WhisperSTT : MonoBehaviour
{
    [Header("API Configuration")]
    public string whisperEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
    private string apiKey;

    [Header("Recording Settings")]
    public int recordingDuration = 5;

    [Header("UI")]
    public TextMeshProUGUI transcriptionOutput;

    private bool isRecording = false;
    private string tempWavFilePath;

    private class TranscriptionResponse { public string text; }
    [System.Serializable] private class APIKeyContainer { public string groq; }

    private void Awake()
    {
        LoadAPIKey();
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

        if (transcriptionOutput)
        {
            transcriptionOutput.gameObject.SetActive(true);
            transcriptionOutput.text = "Listening...";
        }

        AudioClip recordingClip = Microphone.Start(null, false, recordingDuration, 44100);
        yield return new WaitForSeconds(recordingDuration);
        Microphone.End(null);

        SavWav.Save(tempWavFilePath, recordingClip);

        yield return TranscribeCoroutine(tempWavFilePath, onTranscriptionComplete);

        isRecording = false;
    }

    private IEnumerator TranscribeCoroutine(string filePath, System.Action<string> onResult)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("WhisperSTT: API Key is not loaded. Aborting transcription.");
            onResult?.Invoke(null);
            if (transcriptionOutput)
            {
                transcriptionOutput.text = "API key missing.";
                StartCoroutine(HideTranscriptionAfterDelay(2f));
            }
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
                if (transcriptionOutput)
                {
                    transcriptionOutput.text = "Transcription failed.";
                    StartCoroutine(HideTranscriptionAfterDelay(2f));
                }
                onResult?.Invoke(null);
            }
            else
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<TranscriptionResponse>(request.downloadHandler.text);
                    if (transcriptionOutput)
                    {
                        transcriptionOutput.text = response.text;
                        StartCoroutine(HideTranscriptionAfterDelay(2f));
                    }
                    onResult?.Invoke(response.text);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("WhisperSTT: Failed to parse transcription JSON: " + ex.Message);
                    if (transcriptionOutput)
                    {
                        transcriptionOutput.text = "Parsing error.";
                        StartCoroutine(HideTranscriptionAfterDelay(2f));
                    }
                    onResult?.Invoke(null);
                }
            }
        }
    }

    private IEnumerator HideTranscriptionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (transcriptionOutput) transcriptionOutput.gameObject.SetActive(false);
    }
}
