using System.Collections;
using System.Linq;
using UnityEngine;
using PassthroughCameraSamples;

namespace QuestCameraKit.OpenAI
{
    public class VoiceCommandHandler : MonoBehaviour
    {
        [SerializeField] private WebCamTextureManager webcamManager;
        [SerializeField] private ImageOpenAIConnector imageConnector;

        [Header("Image (For testing in Editor)")]
        [Tooltip("Assign a dummy image for testing in the editor or if no webcam is available.")]
        [SerializeField]
        private Texture2D dummyImage;

        private SttManager _sttManager;

        private void Awake()
        {
            _sttManager = FindFirstObjectByType<SttManager>();
            if (_sttManager == null)
            {
                Debug.LogError("STTActivation not found in the scene.");
                return;
            }

            _sttManager.OnTranscriptionComplete += OnTranscriptionReceived;
        }

        private void OnDestroy()
        {
            if (_sttManager != null)
            {
                _sttManager.OnTranscriptionComplete -= OnTranscriptionReceived;
            }
        }

        private void OnTranscriptionReceived(string transcription)
        {
            StartCoroutine(CaptureAndSendImage(transcription));
        }

        private IEnumerator CaptureAndSendImage(string transcription)
        {
            Texture2D capturedTexture;

            if (Application.isEditor || !webcamManager || !webcamManager.WebCamTexture ||
                !webcamManager.WebCamTexture.isPlaying)
            {
                if (dummyImage)
                {
                    capturedTexture = dummyImage;
                }
                else
                {
                    capturedTexture = new Texture2D(512, 512, TextureFormat.RGB24, false);
                    var fillColor = Color.gray;
                    var fillPixels = Enumerable.Repeat(fillColor, 512 * 512).ToArray();
                    capturedTexture.SetPixels(fillPixels);
                    capturedTexture.Apply();
                }
            }
            else
            {
                // Delay so we can avoid having our controller or hand in the image
                yield return new WaitForSeconds(1.0f);
                yield return new WaitForEndOfFrame();
                var webCamTex = webcamManager.WebCamTexture;
                capturedTexture = new Texture2D(webCamTex.width, webCamTex.height, TextureFormat.RGBA32, false);
                capturedTexture.SetPixels(webCamTex.GetPixels());
                capturedTexture.Apply();
            }

            if (imageConnector)
            {
                imageConnector.SendImage(capturedTexture, transcription);
            }
            else
            {
                Debug.LogError("ImageOpenAIConnector not assigned in VoiceCommandHandler.");
            }
        }
    }
}