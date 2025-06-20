// FlashlightController.cs

using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlashlightController : MonoBehaviour
{
    [Header("Core Components")]
    public Light flashlightBeam;
    public GameObject beamMeshObject;

    // --- NEW: Add fields for SFX ---
    [Header("Sound Effects")]
    [Tooltip("The AudioSource component that will play the sound.")]
    public AudioSource audioSource;
    [Tooltip("The sound to play when the flashlight turns on.")]
    public AudioClip turnOnSound;


    public bool IsHeld { get; set; } = false;

    private void Awake()
    {
        // ... (rest of Awake method is the same)
        if (flashlightBeam == null) { flashlightBeam = GetComponent<Light>(); }
        if (flashlightBeam != null) { flashlightBeam.enabled = false; }
        if (beamMeshObject != null) { beamMeshObject.SetActive(false); }
    }

    public void ToggleFlashlight()
    {
        if (flashlightBeam == null || beamMeshObject == null) return;

        Debug.Log($"ToggleFlashlight() called. Current IsHeld state is: {IsHeld}");

        if (flashlightBeam.enabled)
        {
            flashlightBeam.enabled = false;
            beamMeshObject.SetActive(false);
            Debug.Log("Flashlight turned OFF.");
        }
        else if (IsHeld)
        {
            flashlightBeam.enabled = true;
            beamMeshObject.SetActive(true);
            Debug.Log("Flashlight turned ON.");

            // --- NEW: Play the sound effect ---
            if (audioSource != null && turnOnSound != null)
            {
                audioSource.PlayOneShot(turnOnSound);
            }
        }
        else
        {
            Debug.Log("Tried to turn on flashlight, but it is not being held.");
        }
    }
}