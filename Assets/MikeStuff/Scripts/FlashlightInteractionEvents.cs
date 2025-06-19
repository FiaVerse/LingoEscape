// FlashlightInteractionEvents.cs
// Uses the InteractableUnityEventWrapper to update the FlashlightController's 'IsHeld' state.

using UnityEngine;
using Oculus.Interaction; // Required for the interaction events

//[RequireComponent(typeof(FlashlightController))]
[RequireComponent(typeof(InteractableUnityEventWrapper))]
public class FlashlightInteractionEvents : MonoBehaviour
{
    [SerializeField]
    private FlashlightController m_flashlightController;
    private InteractableUnityEventWrapper m_eventWrapper;

    private void Awake()
    {
        //m_flashlightController = GetComponent<FlashlightController>();
        m_eventWrapper = GetComponent<InteractableUnityEventWrapper>();

        if (m_flashlightController == null)
        {
            Debug.LogError("FlashlightInteractionEvents: Could not find FlashlightController component!", this);
        }
        if (m_eventWrapper == null)
        {
            Debug.LogError("FlashlightInteractionEvents: Could not find InteractableUnityEventWrapper component!", this);
        }
    }

    private void OnEnable()
    {
        // Subscribe our custom methods to the Unity Events from the wrapper.
        if (m_eventWrapper != null)
        {
            m_eventWrapper.WhenSelect.AddListener(HandleGrab);
            m_eventWrapper.WhenUnselect.AddListener(HandleRelease);
            Debug.Log("FlashlightInteractionEvents: Subscribed to Select and Unselect events.", this);
        }
    }

    private void OnDisable()
    {
        // Always unsubscribe from events when the object is disabled to prevent errors.
        if (m_eventWrapper != null)
        {
            m_eventWrapper.WhenSelect.RemoveListener(HandleGrab);
            m_eventWrapper.WhenUnselect.RemoveListener(HandleRelease);
        }
    }

    private void HandleGrab()
    {
        // --- DEBUG: This log is crucial. It confirms the grab event is firing. ---
        Debug.Log("HandleGrab() called. Setting IsHeld to TRUE.", this);
        if (m_flashlightController != null)
        {
            m_flashlightController.IsHeld = true;
        }
    }

    private void HandleRelease()
    {
        // --- DEBUG: This log helps check for unexpected releases. ---
        Debug.Log("HandleRelease() called. Setting IsHeld to FALSE.", this);
        if (m_flashlightController != null)
        {
            m_flashlightController.IsHeld = false;
            if (m_flashlightController.flashlightBeam != null)
            {
                m_flashlightController.flashlightBeam.enabled = false;
            }
        }
    }
}
