using UnityEngine;
using System;
using System.Linq;
using System.Text;
using System.Globalization;

public class SpeechValidator : MonoBehaviour
{
    public static SpeechValidator Instance { get; private set; }

    [Header("Services")]
    public WhisperSTT sttService;

    [Header("Validation Settings")]
    [Tooltip("Controls how forgiving the validation is. 0.0 = perfect match required. 0.3 = allows for ~3 errors per 10 letters. A good starting value is 0.25.")]
    [Range(0.0f, 1.0f)]
    public float fuzzyMatchThreshold = 0.25f;

    private string expectedWord;
    private System.Action<bool> onValidationResult;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    public void ListenForWord(string word, System.Action<bool> onResult)
    {
        if (sttService == null) { Debug.LogError("SpeechValidator: WhisperSTT service is not assigned!"); onResult?.Invoke(false); return; }
        if (string.IsNullOrWhiteSpace(word)) { Debug.LogError("SpeechValidator: ListenForWord called with an empty word."); onResult?.Invoke(false); return; }

        this.expectedWord = word;
        this.onValidationResult = onResult;

        Debug.Log($"SpeechValidator: Now listening for the phrase '{expectedWord}'. Starting recording...");
        sttService.StartRecordingAndTranscription(HandleTranscriptionResult);
    }

    private void HandleTranscriptionResult(string transcribedText)
    {
        if (string.IsNullOrEmpty(transcribedText))
        {
            onValidationResult?.Invoke(false);
            return;
        }

        string normalizedTranscribedText = NormalizeString(transcribedText);
        string normalizedExpectedWord = NormalizeString(expectedWord);
        
        // --- NEW: Fuzzy Matching Logic ---

        // 1. Calculate the edit distance between the two strings.
        int distance = CalculateLevenshteinDistance(normalizedTranscribedText, normalizedExpectedWord);

        // 2. Calculate a similarity ratio based on the length of the longer string.
        int longerLength = Mathf.Max(normalizedTranscribedText.Length, normalizedExpectedWord.Length);
        if (longerLength == 0)
        {
            // Both strings are empty, so they are a perfect match.
            onValidationResult?.Invoke(true);
            return;
        }

        float similarity = (float)distance / longerLength;

        // 3. The attempt is correct if the similarity ratio is WITHIN our threshold.
        bool isCorrect = similarity <= fuzzyMatchThreshold;
        
        // --- End of new logic ---

        Debug.Log($"Validation: Expected '{normalizedExpectedWord}', Got '{normalizedTranscribedText}'. Distance: {distance}, Similarity: {similarity:P1}. Correct: {isCorrect}");

        if (isCorrect)
        {
            Debug.Log("SpeechValidator: Correct or close enough phrase spoken!");
        }
        else
        {
            Debug.Log("SpeechValidator: Incorrect phrase spoken.");
        }

        onValidationResult?.Invoke(isCorrect);
    }

    private string NormalizeString(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalized = text.ToLowerInvariant().Trim();
        var sb = new StringBuilder();
        foreach (char c in normalized)
        {
            if (!char.IsPunctuation(c)) sb.Append(c);
        }
        normalized = sb.ToString();
        var formD = normalized.Normalize(NormalizationForm.FormD);
        sb.Clear();
        foreach (char c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// This is the number of edits (insertions, deletions, substitutions) to get from one to the other.
    /// </summary>
    private int CalculateLevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
        if (string.IsNullOrEmpty(b)) return a.Length;

        int lengthA = a.Length;
        int lengthB = b.Length;
        var distances = new int[lengthA + 1, lengthB + 1];

        for (int i = 0; i <= lengthA; distances[i, 0] = i++);
        for (int j = 0; j <= lengthB; distances[0, j] = j++);

        for (int i = 1; i <= lengthA; i++)
        {
            for (int j = 1; j <= lengthB; j++)
            {
                int cost = (b[j - 1] == a[i - 1]) ? 0 : 1;
                distances[i, j] = Mathf.Min(
                    Mathf.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }
        return distances[lengthA, lengthB];
    }
}