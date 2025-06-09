using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    // This makes the manager accessible from any other script.
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject wordPopupPanel; // The parent panel of your text
    public TMP_Text wordPopupText;   // The text element itself

    private void Awake()
    {
        // This is the singleton pattern.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        // Start with the UI hidden.
        if(wordPopupPanel != null)
        {
            wordPopupPanel.SetActive(false);
        }
    }

    // Any script can call this function to show a word.
    public void ShowWordPopup(string word)
    {
        if (wordPopupPanel == null || wordPopupText == null) return;

        wordPopupText.text = word;
        wordPopupPanel.SetActive(true);
    }

    // We can add a function to hide it later if needed.
    public void HideWordPopup()
    {
        if (wordPopupPanel == null) return;

        wordPopupPanel.SetActive(false);
    }
}