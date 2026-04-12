using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class UpcomingQuizClassData
{
    public string subjectName = "New Subject";
    public ClassBoardType boardType = ClassBoardType.Quiz;
    [FormerlySerializedAs("questions")] public QuestionData[] quizQuestions;
    public QuestionData[] studentQuestions;
    public HistoryTimelineClassData historyTimelineData;
}
