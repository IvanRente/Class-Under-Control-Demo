using UnityEngine;

public class AnswerHitZone : MonoBehaviour, IPrimaryClickReceiver
{
    public int answerIndex;
    public QuizBoard quizBoard;

    public void OnPrimaryClick(PlayerController player)
    {
        if (quizBoard == null)
            return;

        quizBoard.AnswerButton(answerIndex);
    }
}
