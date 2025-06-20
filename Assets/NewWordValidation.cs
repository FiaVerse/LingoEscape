using UnityEngine;
using System;

public class NewWordValidation : MonoBehaviour
{
    public event Action<NewWordValidation> OnWordValidated;

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

    //  public void Initialize(string assignedWord, string assignedAudioPrompt, AudioClip clip)
    //   {
    //       word = assignedWord;
    //      audioPrompt = assignedAudioPrompt;
    //       wordAudio = clip;
    //  }

    public void Record()
    {
        if (isValidated) return;
        
        SpeechValidator.Instance.ListenForWord(word, OnValidationResult);
    }
    
    public void PlayHint() // add a button or some way for player to play hints if they need them
    {
        GroqTTS.Instance.Speak(audioPrompt);
        //if (!string.IsNullOrEmpty(audioPrompt) && ElevenlabsTTS.Instance != null)
        //{
       //     ElevenlabsTTS.Instance.Speak(audioPrompt);
       // }
    }
    
    void OnValidationResult(bool isCorrect)
    {
        if (isCorrect && !isValidated)
        {
            // UIManager.Instance.ShowWordPopup(word);
            isValidated = true;
            OnWordValidated?.Invoke(this);
        }
    }

    
    
}


/* audio prompt is the hint now
if (GroqTTS.Instance != null && !string.IsNullOrEmpty(audioPrompt))
{
    GroqTTS.Instance.Speak(audioPrompt);
}
//if (ElevenlabsTTS.Instance != null && !string.IsNullOrEmpty(audioPrompt))
//{
//     ElevenlabsTTS.Instance.Speak(audioPrompt);
// }
*/