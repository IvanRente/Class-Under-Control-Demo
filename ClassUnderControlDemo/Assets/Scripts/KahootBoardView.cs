using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KahootBoardView : MonoBehaviour
{
    public GameObject root;
    public GameObject answersRoot;
    public GameObject graphRoot;
    public TMP_Text questionText;
    public TMP_Text[] answerTexts = new TMP_Text[4];
    public Image[] answerBackgrounds = new Image[4];
    public RectTransform[] graphBars = new RectTransform[4];
    public TMP_Text[] graphValues = new TMP_Text[4];
    public float maxBarHeight = 220f;

    [Header("Colors: Red, Yellow, Blue, Purple")]
    public Color[] optionColors = new Color[4]
    {
        new Color32(230, 57, 70, 255),
        new Color32(242, 170, 0, 255),
        new Color32(0, 116, 217, 255),
        new Color32(138, 43, 226, 255)
    };

    public void HideAll()
    {
        if (root) root.SetActive(false);
    }

    public void ShowQuestion(QuestionData q, int roundIndex, int totalRounds)
    {
        if (root) root.SetActive(true);
        if (answersRoot) answersRoot.SetActive(true);
        if (graphRoot) graphRoot.SetActive(false);

        if (questionText)
            questionText.text = $"Q{roundIndex + 1}/{totalRounds}: {q.question}";

        for (int i = 0; i < 4; i++)
        {
            if (i < answerTexts.Length && answerTexts[i] != null)
                answerTexts[i].text = (q.answers != null && i < q.answers.Length) ? q.answers[i] : "";

            if (i < answerBackgrounds.Length && answerBackgrounds[i] != null && i < optionColors.Length)
                answerBackgrounds[i].color = optionColors[i];
        }
    }

    public void ShowGraph(int[] votes)
    {
        if (answersRoot) answersRoot.SetActive(false);
        if (graphRoot) graphRoot.SetActive(true);

        int maxVotes = 1;
        for (int i = 0; i < votes.Length; i++)
            maxVotes = Mathf.Max(maxVotes, votes[i]);

        for (int i = 0; i < 4; i++)
        {
            float normalized = votes[i] / (float)maxVotes;
            float h = normalized * maxBarHeight;

            if (i < graphBars.Length && graphBars[i] != null)
            {
                Vector2 s = graphBars[i].sizeDelta;
                s.y = h;
                graphBars[i].sizeDelta = s;

                Image img = graphBars[i].GetComponent<Image>();
                if (img != null && i < optionColors.Length)
                    img.color = optionColors[i];
            }

            if (i < graphValues.Length && graphValues[i] != null)
                graphValues[i].text = votes[i].ToString();
        }
    }

    public void ShowFinalMessage(string message)
    {
        if (root) root.SetActive(true);
        if (answersRoot) answersRoot.SetActive(false);
        if (graphRoot) graphRoot.SetActive(false);
        if (questionText) questionText.text = message;
    }
}
