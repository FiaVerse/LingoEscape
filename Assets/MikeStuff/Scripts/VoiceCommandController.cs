// VoiceCommandController.cs (Updated with Basement Lights Command)
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

    [Tooltip("Drag the GameObject that has the SceneLightManager script attached.")]
    [SerializeField]
    private SceneLightManager m_sceneLightManager;

    // NEW: Add a reference to our new light toggling script.
    [Tooltip("Drag the GameObject that has the SceneLightsToggleController script attached.")]
    [SerializeField]
    private SceneLightsToggleController m_sceneLightsToggleController;


    [Header("Voice Command Settings")]
    [SerializeField]
    private string m_blacklightKeyword = "black light";

    [SerializeField]
    private string m_brightnessUpKeyword = "brightness up";

    [SerializeField]
    private string m_brightnessDownKeyword = "brightness down";

    [SerializeField]
    private string m_flashlightKeyword = "flashlight";

    // NEW: Add the keyword for the basement lights command.
    [SerializeField]
    private string m_toggleLightsKeyword = "basement lights";


    void Update()
    {
        // This trigger logic remains the same.
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            m_whisperSTT.StartRecordingAndTranscription(HandleTranscriptionResult);
        }
    }

    private void HandleTranscriptionResult(string transcribedText)
    {
        if (string.IsNullOrWhiteSpace(transcribedText))
        {
            Debug.Log("VoiceCommandController: Received empty or null transcription.");
            return;
        }

        string processedText = transcribedText.Trim().ToLower();
        Debug.Log($"VoiceCommandController: Heard '{processedText}'");

        if (processedText.Contains(m_blacklightKeyword))
        {
            Debug.Log("VoiceCommandController: 'blacklight' keyword detected! Toggling blacklight.");
            m_blacklightController.ToggleBlacklight();
        }
        else if (processedText.Contains(m_brightnessUpKeyword))
        {
            Debug.Log("VoiceCommandController: 'brightness up' keyword detected! Increasing brightness.");
            m_sceneLightManager.IncreaseBrightness();
        }
        else if (processedText.Contains(m_brightnessDownKeyword))
        {
            Debug.Log("VoiceCommandController: 'brightness down' keyword detected! Decreasing brightness.");
            m_sceneLightManager.DecreaseBrightness();
        }
        else if (processedText.Contains(m_flashlightKeyword))
        {
            FlashlightController flashlight = FindAnyObjectByType<FlashlightController>();
            if (flashlight != null)
            {
                Debug.Log("VoiceCommandController: 'flashlight' keyword detected! Toggling flashlight.");
                flashlight.ToggleFlashlight();
            }
            else
            {
                Debug.LogWarning("VoiceCommandController: Tried to toggle flashlight, but no FlashlightController was found in the scene.");
            }
        }
        // NEW: Check for the "basement lights" keyword.
        else if (processedText.Contains(m_toggleLightsKeyword))
        {
            Debug.Log("VoiceCommandController: 'basement lights' keyword detected! Toggling scene lights.");
            if (m_sceneLightsToggleController != null)
            {
                m_sceneLightsToggleController.ToggleLights();
            }
            else
            {
                Debug.LogWarning("VoiceCommandController: SceneLightsToggleController reference is not set.");
            }
        }
    }
}
