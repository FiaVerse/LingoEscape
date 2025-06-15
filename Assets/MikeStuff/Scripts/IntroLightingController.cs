// IntroLightingController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the introductory lighting sequence for the game.
/// Starts with a dark passthrough and no virtual lights, then fades them in on command.
/// </summary>
public class IntroLightingController : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to the OVRPassthroughLayer on your OVRCameraRig.")]
    [SerializeField]
    private OVRPassthroughLayer m_passthroughLayer;

    [Header("Animation Settings")]
    [Tooltip("How dark the scene should be at the start. -1 is max darkness, 0 is normal.")]
    [Range(-1f, 0f)]
    [SerializeField]
    private float m_startBrightness = -0.9f;

    [Tooltip("The duration in seconds for the lights to fade in.")]
    [SerializeField]
    private float m_fadeInDuration = 3.0f;

    // --- Private State ---
    // For storing the original state of the scene's lights
    private List<Light> m_sceneLights;
    private List<float> m_originalIntensities;
    private bool m_introSequenceStarted = false;

    void Start()
    {
        // 1. Find the Passthrough Layer
        if (m_passthroughLayer == null)
        {
            m_passthroughLayer = FindObjectOfType<OVRPassthroughLayer>();
        }
        if (m_passthroughLayer == null)
        {
            Debug.LogError("IntroLightingController: Could not find an OVRPassthroughLayer!", this);
            this.enabled = false;
            return;
        }

        // 2. Find all MRUK lights and store their original intensity
        m_sceneLights = new List<Light>(FindObjectsOfType<Light>());
        m_originalIntensities = new List<float>();
        foreach (var light in m_sceneLights)
        {
            m_originalIntensities.Add(light.intensity);
        }

        // 3. Set the initial dark state
        SetInitialDarkness();
    }

    /// <summary>
    /// Sets the scene to its initial dark state immediately.
    /// </summary>
    void SetInitialDarkness()
    {
        Debug.Log("Setting initial dark state.");
        // Set passthrough to be dark
        m_passthroughLayer.SetBrightnessContrastSaturation(brightness: m_startBrightness);

        // Turn off all the virtual lights
        foreach (var light in m_sceneLights)
        {
            light.intensity = 0f;
        }
    }

    /// <summary>
    /// Call this method from another script or event to begin the fade-in sequence.
    /// </summary>
    public void BeginIntroSequence()
    {
        if (m_introSequenceStarted) return;
        
        m_introSequenceStarted = true;
        Debug.Log("Beginning intro lighting sequence.");
        StartCoroutine(FadeInLights());
    }

    /// <summary>
    /// A coroutine that smoothly transitions brightness and light intensity over a duration.
    /// </summary>
    private IEnumerator FadeInLights()
    {
        float elapsedTime = 0f;

        // Get the starting intensity of the virtual lights (which is 0)
        float startIntensity = 0f;
        
        // The brightness value we are animating in the passthrough layer
        float currentBrightness = m_startBrightness;

        while (elapsedTime < m_fadeInDuration)
        {
            // Calculate the current progress of the fade, from 0 to 1
            float progress = elapsedTime / m_fadeInDuration;

            // --- Animate Passthrough Brightness ---
            // Lerp from the start brightness towards normal brightness (0)
            currentBrightness = Mathf.Lerp(m_startBrightness, 0f, progress);
            m_passthroughLayer.SetBrightnessContrastSaturation(brightness: currentBrightness);

            // --- Animate Virtual Light Intensity ---
            for (int i = 0; i < m_sceneLights.Count; i++)
            {
                // Lerp from 0 towards the light's original intensity
                float targetIntensity = m_originalIntensities[i];
                m_sceneLights[i].intensity = Mathf.Lerp(startIntensity, targetIntensity, progress);
            }

            // Wait for the next frame
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // --- Finalize State ---
        // At the end, explicitly set the final values to ensure they are perfect
        m_passthroughLayer.SetBrightnessContrastSaturation(brightness: 0f); // Normal brightness
        for (int i = 0; i < m_sceneLights.Count; i++)
        {
            m_sceneLights[i].intensity = m_originalIntensities[i]; // Original intensity
        }

        Debug.Log("Intro lighting sequence complete.");
    }
}