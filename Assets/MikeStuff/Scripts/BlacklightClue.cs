// BlacklightClue.cs
using UnityEngine;

/// <summary>
/// Represents a clue object that is only visible when the blacklight effect is active.
/// Place this on the parent GameObject of your hidden message/object.
/// </summary>
public class BlacklightClue : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The actual GameObject with the clue's visuals. This will be enabled/disabled.")]
    private GameObject m_clueObject;

    void Awake()
    {
        // Ensure the clue is hidden by default when the scene starts.
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
    /// Shows or hides the clue. Called by the main BlacklightController.
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