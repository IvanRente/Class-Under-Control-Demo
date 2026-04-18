using System;
using System.Collections.Generic;
using UnityEngine;

public class StudentTypeDecisionTree : MonoBehaviour
{
    public enum StudentType
    {
        Escaping,
        Throwing,
        Annoying,
        Pyromaniac
    }

    [Serializable]
    public class LegacyStudentSlot
    {
        public string slotName;
        public StudentAI escapingStudent;
        public ThrowStudent throwingStudent;
        public AnnoyingStudent annoyingStudent;
        public PyromaniacStudent pyromaniacStudent;
    }

    [Header("Class Progression")]
    public int startingLevel = 1;
    public bool advanceLevelEveryClass = true;
    public bool ensureOneOfEachUnlockedType = true;

    [Header("Weighted Chances")]
    public float escapingWeight = 40f;
    public float throwingWeight = 30f;
    public float annoyingWeight = 20f;
    public float pyromaniacWeight = 10f;

    [Header("Student Prefabs")]
    public GameObject escapingStudentPrefab;
    public GameObject throwingStudentPrefab;
    public GameObject annoyingStudentPrefab;
    public GameObject pyromaniacStudentPrefab;

    [Header("Spawn Points")]
    public Transform[] studentSpawnPoints = new Transform[0];
    public bool randomizeSpawnPointsEveryClass = true;
    public bool useLegacySlotSeatPointsWhenSpawnPointsEmpty = true;
    [HideInInspector] public LegacyStudentSlot[] studentSlots = new LegacyStudentSlot[0];

    [Header("Shared Scene References")]
    public Transform doorPoint;
    public Transform playerTarget;
    public Transform[] firePoints = new Transform[0];
    public bool autoFindPlayerTarget = true;
    public bool disableSceneStudentsBeforeSpawning = true;

    [Header("Roll Call Name Pool")]
    public bool randomizeNamesEveryClass = true;
    public List<string> studentNames = new List<string>
    {
        "Alex",
        "Daniel",
        "German",
        "Jon",
        "Laura",
        "Lucas",
        "Marco",
        "Miguel",
        "Nora",
        "Sofia"
    };

    readonly List<IClassStudent> assignedStudents = new List<IClassStudent>();
    readonly List<GameObject> spawnedStudentObjects = new List<GameObject>();
    readonly List<StudentType> unlockedTypes = new List<StudentType>();
    readonly List<StudentType> availableTypes = new List<StudentType>();
    readonly List<StudentType> selectedTypes = new List<StudentType>();
    readonly List<Transform> resolvedSpawnPoints = new List<Transform>();
    readonly List<string> resolvedNames = new List<string>();
    readonly HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<Transform> seenSpawnPoints = new HashSet<Transform>();

    int preparedClassCount;

    public int CurrentLevel { get; private set; } = 1;
    public bool HasAssignedStudents => assignedStudents.Count > 0;

    public void PrepareRosterForNextClass()
    {
        ClearSpawnedStudents();

        CurrentLevel = Mathf.Max(1, startingLevel + (advanceLevelEveryClass ? preparedClassCount : 0));
        BuildUnlockedTypes(CurrentLevel);
        BuildAvailableTypes();
        BuildSpawnPointPool();
        ResolveSharedReferencesFromLegacySlots();

        if (disableSceneStudentsBeforeSpawning)
            DisableExistingSceneStudents();

        if (resolvedSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[StudentTypeDecisionTree] No student spawn points are assigned.");
            AdvancePreparedClassCount();
            return;
        }

        if (availableTypes.Count == 0)
        {
            Debug.LogWarning($"[StudentTypeDecisionTree] No student prefabs are assigned for level {CurrentLevel}.");
            AdvancePreparedClassCount();
            return;
        }

        BuildSelectedTypes(resolvedSpawnPoints.Count);
        BuildNamePool(selectedTypes.Count);
        SpawnSelectedStudents();
        AdvancePreparedClassCount();
    }

    public bool TryGetAssignedStudents(List<IClassStudent> results)
    {
        if (results == null)
            return false;

        results.Clear();

        for (int i = 0; i < assignedStudents.Count; i++)
        {
            if (ClassStudentUtility.IsStudentActive(assignedStudents[i]))
                results.Add(assignedStudents[i]);
        }

        return results.Count > 0;
    }

    public void ClearSpawnedStudents()
    {
        assignedStudents.Clear();

        for (int i = spawnedStudentObjects.Count - 1; i >= 0; i--)
        {
            GameObject spawnedStudent = spawnedStudentObjects[i];
            if (spawnedStudent == null)
                continue;

            if (Application.isPlaying)
                Destroy(spawnedStudent);
            else
                DestroyImmediate(spawnedStudent);
        }

        spawnedStudentObjects.Clear();
    }

    void BuildUnlockedTypes(int level)
    {
        unlockedTypes.Clear();
        unlockedTypes.Add(StudentType.Escaping);
        unlockedTypes.Add(StudentType.Throwing);

        if (level >= 2)
            unlockedTypes.Add(StudentType.Annoying);
        if (level >= 3)
            unlockedTypes.Add(StudentType.Pyromaniac);
    }

    void BuildAvailableTypes()
    {
        availableTypes.Clear();

        for (int i = 0; i < unlockedTypes.Count; i++)
        {
            StudentType type = unlockedTypes[i];
            if (GetPrefab(type) != null)
                availableTypes.Add(type);
            else
                Debug.LogWarning($"[StudentTypeDecisionTree] Missing prefab for unlocked student type {type}.");
        }
    }

    void BuildSpawnPointPool()
    {
        resolvedSpawnPoints.Clear();
        seenSpawnPoints.Clear();

        if (studentSpawnPoints != null)
        {
            for (int i = 0; i < studentSpawnPoints.Length; i++)
                AddSpawnPoint(studentSpawnPoints[i]);
        }

        if (resolvedSpawnPoints.Count == 0 && useLegacySlotSeatPointsWhenSpawnPointsEmpty)
            AddLegacySeatPoints();

        if (randomizeSpawnPointsEveryClass)
            Shuffle(resolvedSpawnPoints);
    }

    void AddLegacySeatPoints()
    {
        if (studentSlots == null)
            return;

        for (int i = 0; i < studentSlots.Length; i++)
        {
            LegacyStudentSlot slot = studentSlots[i];
            if (slot == null)
                continue;

            AddSpawnPoint(ClassStudentUtility.GetStudentSeatPoint(slot.escapingStudent));
            AddSpawnPoint(ClassStudentUtility.GetStudentSeatPoint(slot.throwingStudent));
            AddSpawnPoint(ClassStudentUtility.GetStudentSeatPoint(slot.annoyingStudent));
            AddSpawnPoint(ClassStudentUtility.GetStudentSeatPoint(slot.pyromaniacStudent));
        }
    }

    void AddSpawnPoint(Transform spawnPoint)
    {
        if (spawnPoint == null || seenSpawnPoints.Contains(spawnPoint))
            return;

        seenSpawnPoints.Add(spawnPoint);
        resolvedSpawnPoints.Add(spawnPoint);
    }

    void ResolveSharedReferencesFromLegacySlots()
    {
        if (studentSlots == null)
            return;

        for (int i = 0; i < studentSlots.Length; i++)
        {
            LegacyStudentSlot slot = studentSlots[i];
            if (slot == null)
                continue;

            if (doorPoint == null && slot.escapingStudent != null)
                doorPoint = slot.escapingStudent.doorPoint;

            if (playerTarget == null)
                playerTarget = GetLegacyPlayerTarget(slot);

            if ((firePoints == null || firePoints.Length == 0) && slot.pyromaniacStudent != null)
                firePoints = slot.pyromaniacStudent.firePoints;
        }
    }

    Transform GetLegacyPlayerTarget(LegacyStudentSlot slot)
    {
        if (slot.escapingStudent != null && slot.escapingStudent.playerTarget != null)
            return slot.escapingStudent.playerTarget;
        if (slot.throwingStudent != null && slot.throwingStudent.playerTarget != null)
            return slot.throwingStudent.playerTarget;
        if (slot.annoyingStudent != null && slot.annoyingStudent.playerTarget != null)
            return slot.annoyingStudent.playerTarget;
        if (slot.pyromaniacStudent != null && slot.pyromaniacStudent.playerTarget != null)
            return slot.pyromaniacStudent.playerTarget;

        return null;
    }

    void BuildSelectedTypes(int studentCount)
    {
        selectedTypes.Clear();

        if (studentCount <= 0)
            return;

        if (ensureOneOfEachUnlockedType)
        {
            for (int i = 0; i < availableTypes.Count && selectedTypes.Count < studentCount; i++)
                selectedTypes.Add(availableTypes[i]);

            if (availableTypes.Count > studentCount)
                Debug.LogWarning("[StudentTypeDecisionTree] There are fewer spawn points than unlocked student types, so not every type can appear.");
        }

        while (selectedTypes.Count < studentCount)
        {
            if (TryPickWeightedType(out StudentType selectedType))
                selectedTypes.Add(selectedType);
            else
                break;
        }

        Shuffle(selectedTypes);
    }

    bool TryPickWeightedType(out StudentType selectedType)
    {
        selectedType = StudentType.Escaping;
        float totalWeight = 0f;

        for (int i = 0; i < availableTypes.Count; i++)
            totalWeight += Mathf.Max(0f, GetWeight(availableTypes[i]));

        if (totalWeight <= 0f)
            return false;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < availableTypes.Count; i++)
        {
            StudentType type = availableTypes[i];
            roll -= Mathf.Max(0f, GetWeight(type));

            if (roll <= 0f)
            {
                selectedType = type;
                return true;
            }
        }

        selectedType = availableTypes[availableTypes.Count - 1];
        return true;
    }

    void BuildNamePool(int requiredCount)
    {
        resolvedNames.Clear();
        seenNames.Clear();

        if (studentNames != null)
        {
            for (int i = 0; i < studentNames.Count; i++)
            {
                string studentName = studentNames[i];
                if (string.IsNullOrWhiteSpace(studentName))
                    continue;

                studentName = studentName.Trim();
                if (seenNames.Add(studentName))
                    resolvedNames.Add(studentName);
            }
        }

        if (randomizeNamesEveryClass)
            Shuffle(resolvedNames);

        int fallbackNumber = 1;
        while (resolvedNames.Count < requiredCount)
        {
            string fallbackName = $"Student {fallbackNumber}";
            fallbackNumber++;

            if (seenNames.Add(fallbackName))
                resolvedNames.Add(fallbackName);
        }
    }

    void SpawnSelectedStudents()
    {
        for (int i = 0; i < selectedTypes.Count; i++)
        {
            Transform spawnPoint = i < resolvedSpawnPoints.Count ? resolvedSpawnPoints[i] : null;
            if (spawnPoint == null)
                continue;

            StudentType type = selectedTypes[i];
            GameObject prefab = GetPrefab(type);
            if (prefab == null)
                continue;

            GameObject spawnedObject = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, transform);
            spawnedObject.name = i < resolvedNames.Count ? resolvedNames[i] : $"Student {i + 1}";
            spawnedObject.SetActive(true);
            spawnedStudentObjects.Add(spawnedObject);

            IClassStudent spawnedStudent = FindClassStudent(spawnedObject);
            if (spawnedStudent == null)
            {
                Debug.LogWarning($"[StudentTypeDecisionTree] Spawned prefab '{prefab.name}' does not have an IClassStudent component.");
                continue;
            }

            ClassStudentUtility.SetStudentName(spawnedStudent, spawnedObject.name);
            ConfigureSpawnedStudent(spawnedStudent, spawnPoint);
            spawnedStudent.PrepareForNewClass();
            assignedStudents.Add(spawnedStudent);
        }
    }

    IClassStudent FindClassStudent(GameObject root)
    {
        if (root == null)
            return null;

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IClassStudent classStudent)
                return classStudent;
        }

        return null;
    }

    void ConfigureSpawnedStudent(IClassStudent student, Transform seatPoint)
    {
        Transform resolvedPlayerTarget = ResolvePlayerTarget();
        ClassStudentUtility.SetStudentSeatPoint(student, seatPoint);

        switch (student)
        {
            case StudentAI escapingStudent:
                escapingStudent.doorPoint = doorPoint;
                escapingStudent.playerTarget = resolvedPlayerTarget;
                break;
            case ThrowStudent throwStudent:
                throwStudent.playerTarget = resolvedPlayerTarget;
                break;
            case AnnoyingStudent annoyingStudent:
                annoyingStudent.playerTarget = resolvedPlayerTarget;
                annoyingStudent.autoFindPlayerTarget = resolvedPlayerTarget == null;
                break;
            case PyromaniacStudent pyromaniacStudent:
                pyromaniacStudent.playerTarget = resolvedPlayerTarget;
                pyromaniacStudent.autoFindPlayerTarget = resolvedPlayerTarget == null;
                if (firePoints != null && firePoints.Length > 0)
                    pyromaniacStudent.firePoints = firePoints;
                break;
        }
    }

    Transform ResolvePlayerTarget()
    {
        if (playerTarget != null || !autoFindPlayerTarget)
            return playerTarget;

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerTarget = player.transform;
            return playerTarget;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            playerTarget = playerObject.transform;

        return playerTarget;
    }

    GameObject GetPrefab(StudentType type)
    {
        switch (type)
        {
            case StudentType.Escaping:
                return escapingStudentPrefab;
            case StudentType.Throwing:
                return throwingStudentPrefab;
            case StudentType.Annoying:
                return annoyingStudentPrefab;
            case StudentType.Pyromaniac:
                return pyromaniacStudentPrefab;
            default:
                return null;
        }
    }

    float GetWeight(StudentType type)
    {
        switch (type)
        {
            case StudentType.Escaping:
                return escapingWeight;
            case StudentType.Throwing:
                return throwingWeight;
            case StudentType.Annoying:
                return annoyingWeight;
            case StudentType.Pyromaniac:
                return pyromaniacWeight;
            default:
                return 0f;
        }
    }

    void DisableExistingSceneStudents()
    {
        DisableSceneStudents(FindObjectsByType<StudentAI>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        DisableSceneStudents(FindObjectsByType<ThrowStudent>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        DisableSceneStudents(FindObjectsByType<AnnoyingStudent>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        DisableSceneStudents(FindObjectsByType<PyromaniacStudent>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    void DisableSceneStudents<T>(T[] students) where T : MonoBehaviour
    {
        for (int i = 0; i < students.Length; i++)
        {
            T student = students[i];
            if (student == null)
                continue;

            student.gameObject.SetActive(false);
        }
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            T item = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = item;
        }
    }

    void AdvancePreparedClassCount()
    {
        if (advanceLevelEveryClass)
            preparedClassCount++;
    }
}
