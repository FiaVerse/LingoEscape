using UnityEngine;
using System;

public class PuzzleWord : MonoBehaviour
{
    public event Action<PuzzleWord> OnWordValidated;

    public string word;
    public string audioPrompt;
    public AudioClip wordAudio;

    private AudioSource audioSource;
    private bool isValidated = false;

    void Awake()
    {
        if (GetComponent<AudioSource>() == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        else
            audioSource = GetComponent<AudioSource>();
    }

    public void Initialize(string assignedWord, string assignedAudioPrompt, AudioClip clip)
    {
        word = assignedWord;
        audioPrompt = assignedAudioPrompt;
        wordAudio = clip;
    }

    public void Interact()
    {
        if (isValidated) return;
        
        UIManager.Instance.ShowWordPopup(word);

       // if (GroqTTS.Instance != null && !string.IsNullOrEmpty(audioPrompt))
        //{
       //     GroqTTS.Instance.Speak(audioPrompt);
       // }
       if (ElevenlabsTTS.Instance != null && !string.IsNullOrEmpty(audioPrompt))
       {
           ElevenlabsTTS.Instance.Speak(audioPrompt);
       }

       SpeechValidator.Instance.ListenForWord(word, OnValidationResult);
    }

    void OnValidationResult(bool isCorrect)
    {
        if (isCorrect && !isValidated)
        {
            isValidated = true;
            OnWordValidated?.Invoke(this);
        }
    }
    
    public void PlayHint() // add a button or some way for player to play hints if they need them
    {
        if (!string.IsNullOrEmpty(audioPrompt) && ElevenlabsTTS.Instance != null)
        {
            ElevenlabsTTS.Instance.Speak(audioPrompt);
        }
    }

    
    
}