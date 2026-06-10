using System;
using System.Collections.Generic;
using UnityEngine;

public class RollCallController : MonoBehaviour
{
    public enum StudentPopulationMode
    {
        AutoDiscover,
        ManualList
    }

    [Serializable]
    public class StudentEntry
    {
        public string name;
        public StudentAI student;
        public ThrowStudent throwStudent;
        public AnnoyingStudent annoyingStudent;
        public PyromaniacStudent pyromaniacStudent;
        public MonoBehaviour studentBehaviour;
        [HideInInspector] public bool present;
        [HideInInspector] public RollCallRowUI rowUI;

        public IClassStudent GetAssignedStudent()
        {
            if (studentBehaviour is IClassStudent behaviourStudent)
                return behaviourStudent;
            if (annoyingStudent != null)
                return annoyingStudent;
            if (pyromaniacStudent != null)
                return pyromaniacStudent;
            if (student != null)
                return student;
            if (throwStudent != null)
                return throwStudent;

            return null;
        }

        public void AssignStudent(IClassStudent assignedStudent)
        {
            student = assignedStudent as StudentAI;
            throwStudent = assignedStudent as ThrowStudent;
            annoyingStudent = assignedStudent as AnnoyingStudent;
            pyromaniacStudent = assignedStudent as PyromaniacStudent;
            studentBehaviour = assignedStudent as MonoBehaviour;
            name = ClassStudentUtility.GetStudentName(assignedStudent);
        }
    }

    public StudentPopulationMode studentPopulationMode = StudentPopulationMode.AutoDiscover;

    public Transform listRoot;
    public RollCallRowUI rowPrefab;
    public StudentTypeDecisionTree studentTypeDecisionTree;

    public Sprite emptySprite;
    public Sprite checkSprite;
    public GameObject uiRoot;

    public MonoBehaviour voiceRecognizerBehaviour;
    IVoiceRecognizer voiceRecognizer;

    Dictionary<string, StudentEntry> byName;
    readonly List<StudentEntry> students = new();
    readonly List<IClassStudent> discoveredStudents = new();

    public bool UsesStudentTypeDecisionTree
    {
        get
        {
            ResolveStudentTypeDecisionTree();
            return studentTypeDecisionTree != null;
        }
    }

    void Awake()
    {
        voiceRecognizer = voiceRecognizerBehaviour as IVoiceRecognizer;
    }

    void Start()
    {
        if (uiRoot == null && listRoot != null)
            uiRoot = listRoot.parent != null ? listRoot.parent.gameObject : listRoot.gameObject;

        if (voiceRecognizer != null)
            voiceRecognizer.OnText += OnVoiceText;
        else
            Debug.LogWarning("[RollCall] No voice recognizer assigned.");

        ResolveStudentTypeDecisionTree();
        PrepareForNewClass();
    }

    void OnDestroy()
    {
        if (voiceRecognizer != null)
        {
            voiceRecognizer.OnText -= OnVoiceText;
            voiceRecognizer.StopListening();
        }
    }

    public void PrepareForNewClass()
    {
        ResolveStudentTypeDecisionTree();
        if (studentTypeDecisionTree != null)
            studentTypeDecisionTree.PrepareRosterForNextClass();

        SetRollCallUiVisible(true);
        RefreshStudentsForCurrentClass();
        BuildDictionary();
        EnsureRowsBuilt();
        ResetAttendanceRows();
        StartListening();
    }

    public void RestartCurrentClass()
    {
        ResolveStudentTypeDecisionTree();
        SetRollCallUiVisible(true);
        RefreshStudentsForCurrentClass();

        foreach (var entry in students)
        {
            if (entry == null)
                continue;

            IClassStudent student = entry.GetAssignedStudent();
            if (student != null)
                student.PrepareForNewClass();
        }

        BuildDictionary();
        EnsureRowsBuilt();
        ResetAttendanceRows();
        StartListening();
    }

    public void PauseRollCall()
    {
        StopListening();
        SetRollCallUiVisible(false);
    }

    public void GetAssignedStudents(List<IClassStudent> results)
    {
        if (results == null)
            return;

        results.Clear();

        foreach (var entry in students)
        {
            IClassStudent student = entry.GetAssignedStudent();
            if (student != null)
                results.Add(student);
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

    void EnsureRowsBuilt()
    {
        bool needsBuild = listRoot == null || rowPrefab == null || listRoot.childCount != students.Count;

        if (!needsBuild)
        {
            foreach (var student in students)
            {
                if (student.rowUI == null)
                {
                    needsBuild = true;
                    break;
                }
            }
        }

        if (needsBuild)
            BuildUI();
    }

    void ResetAttendanceRows()
    {
        foreach (var student in students)
        {
            student.present = false;

            if (student.rowUI != null)
                student.rowUI.Setup(student.name, emptySprite);
        }
    }

    void BuildDictionary()
    {
        byName = new Dictionary<string, StudentEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in students)
        {
            if (string.IsNullOrWhiteSpace(s.name)) continue;
            var key = s.name.Trim();

            if (byName.ContainsKey(key))
            {
                Debug.LogWarning($"[RollCall] Duplicate student name '{key}' detected. Only the first matching student will answer roll call.");
                continue;
            }

            byName.Add(key, s);
        }
    }

    void RefreshStudentsForCurrentClass()
    {
        if (studentTypeDecisionTree != null)
        {
            studentTypeDecisionTree.TryGetAssignedStudents(discoveredStudents);
            PopulateStudentsFromList(discoveredStudents);
            return;
        }

        if (studentPopulationMode == StudentPopulationMode.AutoDiscover)
            AutoPopulateStudentsFromScene();
        else
            NormalizeManualStudents();
    }

    void AutoPopulateStudentsFromScene()
    {
        ClassStudentUtility.GetObjectsImplementing(discoveredStudents);
        PopulateStudentsFromList(discoveredStudents);
    }

    void PopulateStudentsFromList(List<IClassStudent> sourceStudents)
    {
        students.Clear();

        for (int i = 0; i < sourceStudents.Count; i++)
        {
            IClassStudent assignedStudent = sourceStudents[i];
            if (assignedStudent == null)
                continue;

            StudentEntry entry = new StudentEntry();
            entry.AssignStudent(assignedStudent);

            if (string.IsNullOrWhiteSpace(entry.name))
                continue;

            students.Add(entry);
        }

        students.Sort(CompareStudentsByName);
    }

    void ResolveStudentTypeDecisionTree()
    {
        if (studentTypeDecisionTree != null)
            return;

        studentTypeDecisionTree = FindFirstObjectByType<StudentTypeDecisionTree>(FindObjectsInactive.Include);
    }

    void NormalizeManualStudents()
    {
        for (int i = students.Count - 1; i >= 0; i--)
        {
            StudentEntry entry = students[i];
            if (entry == null)
            {
                students.RemoveAt(i);
                continue;
            }

            IClassStudent assignedStudent = entry.GetAssignedStudent();
            if (!ClassStudentUtility.IsStudentActive(assignedStudent))
            {
                students.RemoveAt(i);
                continue;
            }

            entry.AssignStudent(assignedStudent);
        }
    }

    static int CompareStudentsByName(StudentEntry a, StudentEntry b)
    {
        string leftName = a != null ? a.name : string.Empty;
        string rightName = b != null ? b.name : string.Empty;

        int byName = string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        if (byName != 0)
            return byName;

        return 0;
    }

    string[] GetKeywords()
    {
        var list = new List<string>();
        foreach (var s in students)
            if (!string.IsNullOrWhiteSpace(s.name))
                list.Add(s.name.Trim());
        return list.ToArray();
    }

    void StartListening()
    {
        if (voiceRecognizer == null)
            return;

        voiceRecognizer.StopListening();
        string[] keywords = GetKeywords();
        if (keywords.Length == 0)
        {
            Debug.LogWarning("[RollCall] No active students were found for the current class.");
            return;
        }

        voiceRecognizer.StartListening(keywords);
    }

    void StopListening()
    {
        if (voiceRecognizer != null)
            voiceRecognizer.StopListening();
    }

    void SetRollCallUiVisible(bool visible)
    {
        if (uiRoot != null)
            uiRoot.SetActive(visible);
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

        IClassStudent student = entry.GetAssignedStudent();
        if (student != null)
            student.OnNameCalled();

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
        PauseRollCall();
        if (GameManager.I != null)
            GameManager.I.StartClassNow();
    }
}
