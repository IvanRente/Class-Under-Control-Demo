using UnityEngine;

[System.Serializable]
public class QuestionData
{
    [TextArea] public string question;
    public string[] answers;
    public int correctIndex;
}
