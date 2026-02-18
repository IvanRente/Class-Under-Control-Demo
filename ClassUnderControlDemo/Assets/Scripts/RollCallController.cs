using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RollCallController : MonoBehaviour
{
    [Serializable]
    public class StudentEntry
    {
        public string name;
        public StudentAI student;
        [HideInInspector] public bool present;
    }

    public List<StudentEntry> students = new();
    public TMP_Text tabletText;

    public MonoBehaviour voiceRecognizerBehaviour;
    IVoiceRecognizer voiceRecognizer;

    void Awake()
    {
        voiceRecognizer = voiceRecognizerBehaviour as IVoiceRecognizer;
    }

    void Start()
    {
        if (GameManager.I) GameManager.I.classTimerPaused = true;

        if (voiceRecognizer != null)
        {
            voiceRecognizer.OnText += OnVoiceText;
            voiceRecognizer.StartListening(GetKeywords());
        }

        RefreshUI();
    }

    void OnDestroy()
    {
        if (voiceRecognizer != null)
        {
            voiceRecognizer.OnText -= OnVoiceText;
            voiceRecognizer.StopListening();
        }
    }

    string[] GetKeywords()
    {
        var list = new List<string>();
        foreach (var s in students)
            if (!string.IsNullOrWhiteSpace(s.name))
                list.Add(s.name);
        return list.ToArray();
    }

    void OnVoiceText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        string said = text.Trim();
        for (int i = 0; i < students.Count; i++)
        {
            if (students[i].present) continue;

            if (string.Equals(students[i].name, said, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[RollCall] MATCH -> {students[i].name} is present!");
                students[i].present = true;
                if (students[i].student) students[i].student.OnNameCalled();
                RefreshUI();
                CheckAllPresent();
                return;
            }
        }
    }

    void CheckAllPresent()
    {
        for (int i = 0; i < students.Count; i++)
            if (!students[i].present) return;

        if (GameManager.I) GameManager.I.StartClassNow();
    }

    void RefreshUI()
    {
        if (!tabletText) return;

        var lines = new List<string>();
        foreach (var s in students)
        {
            string mark = s.present ? "✅" : "⬜";
            lines.Add($"{mark} {s.name}");
        }
        tabletText.text = string.Join("\n", lines);

        tabletText.ForceMeshUpdate(true);
        Debug.Log("[RollCall] UI updated:\n" + tabletText.text);
    }
}
