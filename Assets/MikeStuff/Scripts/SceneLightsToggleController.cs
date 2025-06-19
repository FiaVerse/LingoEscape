// SceneLightsToggleController.cs
// Finds all scene lights and provides a method to instantly toggle them on or off,
// including the passthrough brightness for a complete "dark room" effect.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SceneLightsToggleController : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Drag the OVRPassthroughLayer component from your OVRCameraRig here.")]
    [SerializeField]
    private OVRPassthroughLayer m_passthroughLayer;

    [Header("Effect Settings")]
    [Tooltip("How dark the passthrough view should become when lights are off. -1 is max darkness, 0 is normal.")]
    [Range(-1f, 0f)]
    [SerializeField]
    private float m_darknessLevel = -0.9f;

    // --- Private State ---
    private List<Light> m_sceneLights;
    private List<float> m_originalIntensities;
    // Assume lights are on by default after the intro sequence completes.
    private bool m_lightsAreOn = true;

    void Start()
    {
        // Find the passthrough layer if it wasn't assigned in the inspector
        if (m_passthroughLayer == null)
        {
            m_passthroughLayer = FindAnyObjectByType<OVRPassthroughLayer>();
        }
        if (m_passthroughLayer == null)
        {
            Debug.LogError("SceneLightsToggleController: OVRPassthroughLayer reference is missing!", this);
        }

        // We wait for a brief moment before storing the light intensities.
        // This ensures that the IntroLightingController has finished its fade-in sequence,
        // so we save the final, correct brightness as the "original" state.
        StartCoroutine(InitializeLightsAfterDelay(0.5f));
    }

    private IEnumerator InitializeLightsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        m_sceneLights = new List<Light>(FindObjectsByType<Light>(FindObjectsSortMode.None));
        m_originalIntensities = new List<float>();

        foreach (var light in m_sceneLights)
        {
            m_originalIntensities.Add(light.intensity);
        }
        Debug.Log($"SceneLightsToggleController: Stored initial intensity for {m_sceneLights.Count} lights.");
    }

    /// <summary>
    /// Toggles all scene lights and passthrough brightness.
    /// </summary>
    public void ToggleLights()
    {
        if (m_sceneLights == null || m_originalIntensities == null)
        {
            Debug.LogWarning("SceneLightsToggleController: Lights not initialized yet. Cannot toggle.");
            return;
        }
        if (m_passthroughLayer == null)
        {
             Debug.LogError("SceneLightsToggleController: OVRPassthroughLayer reference is missing! Cannot change brightness.", this);
             return;
        }

        m_lightsAreOn = !m_lightsAreOn;
        Debug.Log($"Toggling scene lights. New state is ON: {m_lightsAreOn}");

        // UPDATED: Determine the target brightness for the passthrough layer.
        float targetBrightness = m_lightsAreOn ? 0f : m_darknessLevel;
        m_passthroughLayer.SetBrightnessContrastSaturation(brightness: targetBrightness);

        // This part remains the same: toggle the intensity of virtual lights.
        for (int i = 0; i < m_sceneLights.Count; i++)
        {
            if (m_sceneLights[i] != null)
            {
                // ADD THIS CHECK: If the light has the "Flashlight" tag, skip it.
                if (m_sceneLights[i].CompareTag("Flashlight"))
                {
                    continue; // Skips to the next light in the loop
                }

                m_sceneLights[i].intensity = m_lightsAreOn ? m_originalIntensities[i] : 0f;
            }
        }
    }
}
