// BlacklightClue.cs
// This script now actively registers itself with the BlacklightController when it's created.

using UnityEngine;

public class BlacklightClue : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The actual GameObject with the clue's visuals. This will be enabled/disabled.")]
    private GameObject m_clueObject;

    void OnEnable()
    {
        // When this clue object is created and enabled, find the BlacklightController instance
        // and register this clue with it.
        if (BlacklightController.Instance != null)
        {
            BlacklightController.Instance.RegisterClue(this);
        }
    }

    void OnDisable()
    {
        // When this clue object is about to be destroyed or disabled, unregister it
        // from the controller to keep the list clean.
        if (BlacklightController.Instance != null)
        {
            BlacklightController.Instance.UnregisterClue(this);
        }
    }

    void Awake()
    {
        // Ensure the clue is hidden by default when it is first created.
        if (m_clueObject != null)
        {
            m_clueObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("BlacklightClue: No 'clueObject' has been assigned!", this);
        }
    }

    /// <summary>
    /// Shows or hides the clue. Called by the BlacklightController.
    /// </summary>
    /// <param name="isRevealed">If true, show the clue. Otherwise, hide it.</param>
    public void SetRevealed(bool isRevealed)
    {
        if (m_clueObject != null)
        {
            m_clueObject.SetActive(isRevealed);
        }
    }
}