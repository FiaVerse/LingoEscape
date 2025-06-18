// SceneLightManager.cs
// Finds all lights in the scene and provides methods to adjust their collective brightness.

using UnityEngine;
using System.Collections.Generic;

public class SceneLightManager : MonoBehaviour
{
    [Header("Brightness Settings")]
    [Tooltip("How much the brightness changes with each command (e.g., 0.2 is a 20% step).")]
    [Range(0.05f, 0.5f)]
    public float brightnessStep = 0.2f;

    [Tooltip("The minimum brightness multiplier (e.g., 0.1 is 10% of original brightness).")]
    [Range(0.0f, 1.0f)]
    public float minBrightnessMultiplier = 0.1f;

    [Tooltip("The maximum brightness multiplier (e.g., 2.0 is 200% of original brightness).")]
    [Range(1.0f, 5.0f)]
    public float maxBrightnessMultiplier = 2.0f;

    // --- Private State ---
    private List<Light> m_sceneLights;
    private List<float> m_originalIntensities;
    private float m_currentBrightnessMultiplier = 1.0f; // Start at 100% brightness

    void Start()
    {
        // Find all Light components in the scene at startup
        m_sceneLights = new List<Light>(FindObjectsOfType<Light>());
        m_originalIntensities = new List<float>();

        // Store the original intensity of each light so we have a baseline to return to
        foreach (var light in m_sceneLights)
        {
            m_originalIntensities.Add(light.intensity);
        }

        Debug.Log($"SceneLightManager: Found and stored initial intensity for {m_sceneLights.Count} lights.");
    }

    /// <summary>
    /// Increases the brightness of all scene lights.
    /// </summary>
    public void IncreaseBrightness()
    {
        // Increase the multiplier, but clamp it to the max value
        m_currentBrightnessMultiplier = Mathf.Clamp(m_currentBrightnessMultiplier + brightnessStep, minBrightnessMultiplier, maxBrightnessMultiplier);
        Debug.Log($"Increasing brightness. New multiplier: {m_currentBrightnessMultiplier}");
        ApplyBrightness();
    }

    /// <summary>
    /// Decreases the brightness of all scene lights.
    /// </summary>
    public void DecreaseBrightness()
    {
        // Decrease the multiplier, but clamp it to the min value
        m_currentBrightnessMultiplier = Mathf.Clamp(m_currentBrightnessMultiplier - brightnessStep, minBrightnessMultiplier, maxBrightnessMultiplier);
        Debug.Log($"Decreasing brightness. New multiplier: {m_currentBrightnessMultiplier}");
        ApplyBrightness();
    }

    /// <summary>
    /// Applies the current brightness multiplier to all scene lights.
    /// </summary>
    private void ApplyBrightness()
    {
        if (m_sceneLights == null) return;

        // Loop through all the lights and set their intensity relative to their original value
        for (int i = 0; i < m_sceneLights.Count; i++)
        {
            if (m_sceneLights[i] != null)
            {
                m_sceneLights[i].intensity = m_originalIntensities[i] * m_currentBrightnessMultiplier;
            }
        }
    }
}