using System.Collections.Generic;
using UnityEngine;

public static class ClassStudentUtility
{
    public static void GetObjectsImplementing<T>(List<T> results) where T : class
    {
        if (results == null)
            return;

        results.Clear();

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.isActiveAndEnabled)
                continue;

            T match = behaviour as T;
            if (match != null)
                results.Add(match);
        }
    }

    public static int CountObjectsImplementing<T>() where T : class
    {
        int count = 0;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.isActiveAndEnabled)
                continue;

            if (behaviour is T)
                count++;
        }

        return count;
    }

    public static bool IsStudentActive(IClassStudent student)
    {
        MonoBehaviour behaviour = student as MonoBehaviour;
        return behaviour != null && behaviour.isActiveAndEnabled;
    }

    public static string GetStudentName(IClassStudent student)
    {
        if (student == null)
            return string.Empty;

        string configuredName = student switch
        {
            StudentAI regularStudent => regularStudent.studentName,
            ThrowStudent throwStudent => throwStudent.studentName,
            AnnoyingStudent annoyingStudent => annoyingStudent.studentName,
            PyromaniacStudent pyromaniacStudent => pyromaniacStudent.studentName,
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(configuredName))
            return configuredName.Trim();

        MonoBehaviour behaviour = student as MonoBehaviour;
        return behaviour != null ? behaviour.gameObject.name.Trim() : string.Empty;
    }

}
