using UnityEngine;
using Oculus.Interaction;
using UnityEngine.SceneManagement;
// starts game when language, level and voice is selected 
public class PlayerSelection : MonoBehaviour
{
    public enum OptionType { Language, Level, Voice }

    [Header("Option Settings")]
    public OptionType optionType = OptionType.Language;
    public string     optionValue = "French";   // e.g. "English", "B2", "kid"

    [Header("Scene to Load After All Choices")]
    public string gameSceneName = "Main Lingo";

    // -------------------------------------------------------------
    // This method will be called from the button’s UnityEvent
    // -------------------------------------------------------------
    public void SelectOption()
    {
        switch (optionType)
        {
            case OptionType.Language: PlayerSettings.Language = optionValue; break;
            case OptionType.Level:    PlayerSettings.Level    = optionValue; break;
            case OptionType.Voice:    PlayerSettings.Voice    = optionValue; break;
        }

        Debug.Log($"[Menu] {optionType} set to {optionValue}");

        if (PlayerSettings.IsComplete)
        {
            Debug.Log("[Menu] All selections made – loading game scene.");
            SceneManager.LoadScene("MainLingo");
        }
    }
}