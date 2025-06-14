using UnityEngine;

// This line is a good practice. It ensures that any object with this script
// MUST also have a PuzzleWord script, preventing errors.
[RequireComponent(typeof(PuzzleWord))]
public class XRPuzzleObject : MonoBehaviour
{
    private PuzzleWord puzzleWord;

    private void Awake()
    {
        // Get the PuzzleWord component once when the object wakes up.
        // This is more efficient than searching for it every time.
        puzzleWord = GetComponent<PuzzleWord>();
    }

    /// <summary>
    /// This method should be called by your XR Interaction event (e.g., OnSelect, OnGrab).
    /// </summary>
    public void OnGrabEvent()
    {
        if (puzzleWord != null)
        {
            // Instead of calling the manager with the object's name,
            // we tell this object's own PuzzleWord component to handle the interaction.
            Debug.Log($"XRPuzzleObject on {gameObject.name} was grabbed. Calling Interact().");
            puzzleWord.Interact();
        }
        else
        {
            Debug.LogError($"XRPuzzleObject on {gameObject.name} is missing a required PuzzleWord component!", this.gameObject);
        }
    }
}