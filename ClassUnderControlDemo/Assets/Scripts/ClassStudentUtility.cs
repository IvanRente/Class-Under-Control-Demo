using System.Collections.Generic;
using UnityEngine;

public static class ClassStudentUtility
{
    public static void GetObjectsImplementing<T>(List<T> results) where T : class
    {
        results.Clear();

        MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            T match = behaviours[i] as T;
            if (match != null)
                results.Add(match);
        }
    }

    public static int CountObjectsImplementing<T>() where T : class
    {
        int count = 0;
        MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T)
                count++;
        }

        return count;
    }
}
