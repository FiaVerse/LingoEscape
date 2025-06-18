// PassthroughBlacklightEffect.cs (Corrected for your SDK version)
using UnityEngine;

public class PassthroughBlacklightEffect : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Reference to the OVRPassthroughLayer component on your OVRCameraRig.")]
    private OVRPassthroughLayer m_passthroughLayer;

    [Header("Blacklight Settings")]
    [Tooltip("The color tint to apply to the passthrough feed for the blacklight effect.")]
    [SerializeField]
    private Color m_blacklightTintColor = new Color(0.5f, 0.2f, 1.0f);

    void Start()
    {
        if (m_passthroughLayer == null)
        {
            // UPDATED: Using the new FindAnyObjectByType for better performance.
            m_passthroughLayer = FindAnyObjectByType<OVRPassthroughLayer>();
        }

        if (m_passthroughLayer == null)
        {
            Debug.LogError("PassthroughBlacklightEffect: Could not find an OVRPassthroughLayer in the scene!", this);
            this.enabled = false;
            return;
        }

        SetEffect(false);
    }

    public void SetEffect(bool isEffectOn)
    {
        if (m_passthroughLayer == null) return;

        if (isEffectOn)
        {
            m_passthroughLayer.overridePerLayerColorScaleAndOffset = true;
            m_passthroughLayer.colorScale = new Vector4(m_blacklightTintColor.r, m_blacklightTintColor.g, m_blacklightTintColor.b, 1.0f);
            m_passthroughLayer.colorOffset = Vector4.zero;
        }
        else
        {
            m_passthroughLayer.overridePerLayerColorScaleAndOffset = false;
            m_passthroughLayer.colorScale = Vector4.one;
            m_passthroughLayer.colorOffset = Vector4.zero;
        }
    }
}