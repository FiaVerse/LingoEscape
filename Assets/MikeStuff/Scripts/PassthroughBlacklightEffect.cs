// PassthroughBlacklightEffect.cs
using UnityEngine;

/// <summary>
/// Manages the passthrough visual effect to simulate a blacklight.
/// This script interacts with the OVRPassthroughLayer on the OVRCameraRig
/// using the colorScale and colorOffset properties available in your SDK version.
/// </summary>
public class PassthroughBlacklightEffect : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Reference to the OVRPassthroughLayer component on your OVRCameraRig.")]
    private OVRPassthroughLayer m_passthroughLayer;

    [Header("Blacklight Settings")]
    [Tooltip("The color tint to apply to the passthrough feed for the blacklight effect.")]
    [SerializeField]
    private Color m_blacklightTintColor = new Color(0.5f, 0.2f, 1.0f); // A nice purple tint

    void Start()
    {
        // If the passthrough layer isn't assigned, try to find it automatically.
        if (m_passthroughLayer == null)
        {
            m_passthroughLayer = FindObjectOfType<OVRPassthroughLayer>();
        }

        if (m_passthroughLayer == null)
        {
            Debug.LogError("PassthroughBlacklightEffect: Could not find an OVRPassthroughLayer in the scene!", this);
            this.enabled = false; // Disable script if it can't function.
            return;
        }
        
        // Ensure the effect is off when the game starts
        SetEffect(false);
    }

    /// <summary>
    /// Enables or disables the blacklight color grading on the real-world view.
    /// </summary>
    /// <param name="isEffectOn">True to enable the blacklight tint, false to revert to normal.</param>
    public void SetEffect(bool isEffectOn)
    {
        if (m_passthroughLayer == null) return;

        if (isEffectOn)
        {
            // Enable the override to allow our color changes to take effect.
            m_passthroughLayer.overridePerLayerColorScaleAndOffset = true;
            
            // Set the colorScale. This value multiplies the passthrough camera's colors.
            // We use our tint color, keeping alpha at 1 (fully opaque).
            m_passthroughLayer.colorScale = new Vector4(m_blacklightTintColor.r, m_blacklightTintColor.g, m_blacklightTintColor.b, 1.0f);
            
            // colorOffset is added after scaling. We'll leave it at zero.
            m_passthroughLayer.colorOffset = Vector4.zero;
        }
        else
        {
            // Disable the override to return to the default passthrough appearance.
            m_passthroughLayer.overridePerLayerColorScaleAndOffset = false;
            
            // It's good practice to also reset the values to their defaults.
            m_passthroughLayer.colorScale = Vector4.one;
            m_passthroughLayer.colorOffset = Vector4.zero;
        }
    }
}