using System;
using System.Collections.Generic;
using UnityEngine;

public class RollCallController : MonoBehaviour
{
    [Serializable]
    public class StudentEntry
    {
        public string name;
        public StudentAI student;
        public ThrowStudent throwStudent;
        [HideInInspector] public bool present;
        [HideInInspector] public RollCallRowUI rowUI;
    }

    public List<StudentEntry> students = new();

    public Transform listRoot;
    public RollCallRowUI rowPrefab;

    public Sprite emptySprite;
    public Sprite checkSprite;

    public MonoBehaviour voiceRecognizerBehaviour;
    IVoiceRecognizer voiceRecognizer;

    Dictionary<string, StudentEntry> byName;

    void Awake()
    {
        voiceRecognizer = voiceRecognizerBehaviour as IVoiceRecognizer;
    }

    void Start()
    {
        BuildUI();
        BuildDictionary();

        if (voiceRecognizer != null)
        {
            voiceRecognizer.OnText += OnVoiceText;
            voiceRecognizer.StartListening(GetKeywords());
        }
        else
        {
            Debug.LogWarning("[RollCall] No voice recognizer assigned.");
        }
    }

    void OnDestroy()
    {
        if (voiceRecognizer != null)
        {
            voiceRecognizer.OnText -= OnVoiceText;
            voiceRecognizer.StopListening();
        }
    }

    void BuildUI()
    {
        if (!listRoot || !rowPrefab)
        {
            Debug.LogError("[RollCall] Missing listRoot or rowPrefab.");
            return;
        }

        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        foreach (var s in students)
        {
            s.present = false;

            var row = Instantiate(rowPrefab, listRoot);
            s.rowUI = row;

            row.Setup(s.name, emptySprite);
        }
    }

    void BuildDictionary()
    {
        byName = new Dictionary<string, StudentEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in students)
        {
            if (string.IsNullOrWhiteSpace(s.name)) continue;
            var key = s.name.Trim();

            byName[key] = s;
        }
    }

    string[] GetKeywords()
    {
        var list = new List<string>();
        foreach (var s in students)
            if (!string.IsNullOrWhiteSpace(s.name))
                list.Add(s.name.Trim());
        return list.ToArray();
    }

    void OnVoiceText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        string said = text.Trim();

        Debug.Log($"[VOICE] Detected: '{said}'");

        if (!byName.TryGetValue(said, out var entry) || entry == null)
        {
            Debug.Log($"[RollCall] No match for '{said}'");
            return;
        }

        if (entry.present)
        {
            Debug.Log($"[RollCall] '{said}' already present.");
            return;
        }

        entry.present = true;

        if (entry.rowUI) entry.rowUI.SetPresent(true, emptySprite, checkSprite);

        if (entry.student) entry.student.OnNameCalled();
        if (entry.throwStudent) entry.throwStudent.OnNameCalled();

        if (AllPresent()) StartClass();
    }

    bool AllPresent()
    {
        foreach (var s in students)
            if (!s.present) return false;
        return true;
    }

    void StartClass()
    {
        Debug.Log("[RollCall] All students present! Starting class...");
        if (GameManager.I != null)
        GameManager.I.StartClassNow();
    }
}
