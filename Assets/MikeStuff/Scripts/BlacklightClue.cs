// BlacklightClue.cs
// This script now uses physics triggers to reveal a hidden object
// only when the flashlight beam is pointed at it.

using UnityEngine;

// This ensures the GameObject has a Collider component to work with triggers.
[RequireComponent(typeof(Collider))]
public class BlacklightClue : MonoBehaviour
{
    [Tooltip("The actual GameObject with the clue's visuals that will be enabled/disabled.")]
    [SerializeField]
    private GameObject m_clueObject;

    void Awake()
    {
        // Ensure the clue is hidden at the start.
        if (m_clueObject != null)
        {
            m_clueObject.SetActive(false);
        }

        // IMPORTANT: Make sure the collider on this object is set to be a trigger
        // so it can detect other triggers entering its space without causing a physical collision.
        GetComponent<Collider>().isTrigger = true;
    }

    // This function is called automatically by Unity when another trigger collider enters this one.
    private void OnTriggerEnter(Collider other)
    {
        // We check if the object that entered our trigger is the flashlight beam.
        // We identify the beam by its "FlashlightBeam" tag.
        if (other.CompareTag("FlashlightBeam"))
        {
            Reveal();
        }
    }

    // This function is called automatically by Unity when the other trigger collider exits.
    private void OnTriggerExit(Collider other)
    {
        // If the flashlight beam moves away, we hide the clue again.
        if (other.CompareTag("FlashlightBeam"))
        {
            Hide();
        }
    }

    private void Reveal()
    {
        if (m_clueObject != null)
        {
            m_clueObject.SetActive(true);
            Debug.Log($"Clue '{gameObject.name}' is being revealed.");
        }
    }

    private void Hide()
    {
        if (m_clueObject != null)
        {
            m_clueObject.SetActive(false);
            Debug.Log($"Clue '{gameObject.name}' is being hidden.");
        }
    }
}
