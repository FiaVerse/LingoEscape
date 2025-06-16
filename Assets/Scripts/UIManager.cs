using UnityEngine;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject wordPopupPanel; 
    public TMP_Text wordPopupText;

    private Coroutine hideCoroutine;

    private void Awake()
    {
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
        if (wordPopupPanel != null)
        {
            wordPopupPanel.SetActive(false);
        }
    }

    public void ShowWordPopup(string word)
    {
        if (wordPopupPanel == null || wordPopupText == null) return;

        wordPopupText.text = word;
        wordPopupPanel.SetActive(true);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(ClearWordAfterDelay(2f));
    }

    private IEnumerator ClearWordAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        wordPopupText.text = ""; // Clears the word but keeps panel visible
    }

    public void HideWordPopup()
    {
        if (wordPopupPanel == null) return;

        wordPopupPanel.SetActive(false);
    }
}