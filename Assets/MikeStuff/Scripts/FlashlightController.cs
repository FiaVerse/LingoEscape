// FlashlightController.cs
// Manages the state of the flashlight, including its light component and whether it's being held.

using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlashlightController : MonoBehaviour
{
    [Tooltip("The Light component that acts as the flashlight beam.")]
    public Light flashlightBeam;

    public bool IsHeld { get; set; } = false;

    private void Awake()
    {
        if (flashlightBeam == null)
        {
            flashlightBeam = GetComponent<Light>();
        }

        if (flashlightBeam != null)
        {
            flashlightBeam.enabled = false;
        }
    }

    public void ToggleFlashlight()
    {
        if (flashlightBeam == null) return;

        // --- DEBUG: Log the state of IsHeld right when the command is received. ---
        Debug.Log($"ToggleFlashlight() called. Current IsHeld state is: {IsHeld}");

        if (flashlightBeam.enabled)
        {
            flashlightBeam.enabled = false;
            Debug.Log("Flashlight turned OFF.");
        }
        else if (IsHeld)
        {
            flashlightBeam.enabled = true;
            Debug.Log("Flashlight turned ON.");
        }
        else
        {
            Debug.Log("Tried to turn on flashlight, but it is not being held.");
        }
    }
}