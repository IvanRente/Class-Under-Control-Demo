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

        MonoBehaviour behaviour = student as MonoBehaviour;
        return behaviour != null ? behaviour.gameObject.name.Trim() : string.Empty;
    }

    public static void SetStudentName(IClassStudent student, string studentName)
    {
        MonoBehaviour behaviour = student as MonoBehaviour;
        if (behaviour != null && !string.IsNullOrWhiteSpace(studentName))
            behaviour.gameObject.name = studentName.Trim();
    }

    public static Transform GetStudentSeatPoint(IClassStudent student)
    {
        return student switch
        {
            StudentAI regularStudent => regularStudent.seatPoint,
            ThrowStudent throwStudent => throwStudent.seatPoint,
            AnnoyingStudent annoyingStudent => annoyingStudent.seatPoint,
            PyromaniacStudent pyromaniacStudent => pyromaniacStudent.seatPoint,
            _ => null
        };
    }

    public static void SetStudentSeatPoint(IClassStudent student, Transform seatPoint)
    {
        if (student == null || seatPoint == null)
            return;

        switch (student)
        {
            case StudentAI regularStudent:
                regularStudent.seatPoint = seatPoint;
                break;
            case ThrowStudent throwStudent:
                throwStudent.seatPoint = seatPoint;
                break;
            case AnnoyingStudent annoyingStudent:
                annoyingStudent.seatPoint = seatPoint;
                break;
            case PyromaniacStudent pyromaniacStudent:
                pyromaniacStudent.seatPoint = seatPoint;
                break;
        }
    }

}
