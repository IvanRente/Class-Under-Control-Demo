using System;

public interface IVoiceRecognizer
{
    event Action<string> OnText;
    void StartListening(string[] keywords);
    void StopListening();
}
