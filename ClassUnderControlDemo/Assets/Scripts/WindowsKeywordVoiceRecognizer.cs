using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class WindowsKeywordVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
{
    public event Action<string> OnText;

    KeywordRecognizer recognizer;
    Dictionary<string, Action> actions;

    public void StartListening(string[] keywords)
    {
        StopListening();
        if (keywords == null || keywords.Length == 0)
            return;

        actions = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in keywords)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            string kk = k.Trim();
            if (!actions.ContainsKey(kk))
                actions.Add(kk, () => OnText?.Invoke(kk));
        }

        if (actions.Count == 0)
            return;

        recognizer = new KeywordRecognizer(new List<string>(actions.Keys).ToArray());

        recognizer.OnPhraseRecognized += (args) =>
        {
            Debug.Log($"[Voice] Recognized keyword: '{args.text}'");
            if (actions.TryGetValue(args.text, out var a)) a?.Invoke();
        };
        recognizer.Start();
    }

    public void StopListening()
    {
        if (recognizer != null)
        {
            if (recognizer.IsRunning) recognizer.Stop();
            recognizer.Dispose();
            recognizer = null;
        }
    }

    void OnDisable() => StopListening();
}
