using UnityEngine;

public class GameManager : MonoBehaviour
{
    
    public PuzzleGenerator puzzleGenerator;

    void Start()
    {
        if (puzzleGenerator == null)
        {
            Debug.LogError("GameManager: PuzzleGenerator reference is not set!");
            return;
        }
        
        Debug.Log("GameManager is ready. Call StartNewGame() to begin.");
    }

    public void StartNewGame()
    {
        Debug.Log("StartNewGame() called! Beginning puzzle generation...");
        // maybe play a more detailed narration and a tutorial first then generate puzzles
        puzzleGenerator.StartPuzzleGeneration();

        //  hide the "Start" button after it's pressed
    }
}