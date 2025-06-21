using System.IO;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

namespace QuestCameraKit.OpenAI
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayer : MonoBehaviour
    {
        private AudioSource _audioSource;
        private const bool DeleteCachedFile = true;

        private void OnEnable()
        {
            if (!_audioSource) this._audioSource = GetComponent<AudioSource>();
        }

        private void OnValidate() => OnEnable();

        public void ProcessAudioBytes(byte[] audioData)
        {
            string filePath = Path.Combine(Application.persistentDataPath, "audio.mp3");
            File.WriteAllBytes(filePath, audioData);

            StartCoroutine(LoadAndPlayAudio(filePath));
        }

        private IEnumerator LoadAndPlayAudio(string filePath)
        {
            using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                _audioSource.clip = audioClip;
                _audioSource.Play();
            }
            else Debug.LogError("Audio file loading error: " + www.error);

            if (DeleteCachedFile) File.Delete(filePath);
        }
    }
}