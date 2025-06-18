// VoiceCommandController.cs (Updated with Brightness Commands)
// Listens for voice command triggers, initiates transcription, and acts on the result.

using UnityEngine;

public class VoiceCommandController : MonoBehaviour
{
    [Header("Required Components")]
    [Tooltip("Drag the GameObject that has the WhisperSTT script attached.")]
    [SerializeField]
    private WhisperSTT m_whisperSTT;

    [Tooltip("Drag the GameObject that has the BlacklightController script attached.")]
    [SerializeField]
    private BlacklightController m_blacklightController;

    // NEW: Add a reference to our new SceneLightManager
    [Tooltip("Drag the GameObject that has the SceneLightManager script attached.")]
    [SerializeField]
    private SceneLightManager m_sceneLightManager;


    [Header("Voice Command Settings")]
    [Tooltip("The keyword that will trigger the blacklight effect.")]
    [SerializeField]
    private string m_blacklightKeyword = "blacklight";

    // NEW: Add keywords for brightness control
    [Tooltip("The keyword phrase to increase brightness.")]
    [SerializeField]
    private string m_brightnessUpKeyword = "brightness up";

    [Tooltip("The keyword phrase to decrease brightness.")]
    [SerializeField]
    private string m_brightnessDownKeyword = "brightness down";


    void Update()
    {
        // The trigger remains the same: the 'A' button on the right controller.
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            m_whisperSTT.StartRecordingAndTranscription(HandleTranscriptionResult);
        }
    }

    /// <summary>
    /// This function is called by WhisperSTT when the transcription is complete.
    /// </summary>
    /// <param name="transcribedText">The text returned from the speech-to-text API.</param>
    private void HandleTranscriptionResult(string transcribedText)
    {
        if (string.IsNullOrWhiteSpace(transcribedText))
        {
            Debug.Log("VoiceCommandController: Received empty or null transcription.");
            return;
        }

        // Process the text once to make all checks simpler and case-insensitive
        string processedText = transcribedText.Trim().ToLower();
        Debug.Log($"VoiceCommandController: Heard '{processedText}'");
        
        // Check for the blacklight keyword
        if (processedText.Contains(m_blacklightKeyword))
        {
            Debug.Log("VoiceCommandController: 'blacklight' keyword detected! Toggling blacklight.");
            m_blacklightController.ToggleBlacklight();
        }
        // NEW: Check for the "brightness up" keyword
        else if (processedText.Contains(m_brightnessUpKeyword))
        {
            Debug.Log("VoiceCommandController: 'brightness up' keyword detected! Increasing brightness.");
            m_sceneLightManager.IncreaseBrightness();
        }
        // NEW: Check for the "brightness down" keyword
        else if (processedText.Contains(m_brightnessDownKeyword))
        {
            Debug.Log("VoiceCommandController: 'brightness down' keyword detected! Decreasing brightness.");
            m_sceneLightManager.DecreaseBrightness();
        }
    }
}