using UnityEngine;

public class InstructionManager : MonoBehaviour
{
    public IntroLightingController lightingController;

    // Call this when your instructions are done
    public void OnInstructionsComplete()
    {
        lightingController.BeginIntroSequence();
    }
}