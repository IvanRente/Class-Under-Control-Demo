using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RollCallRowUI : MonoBehaviour
{
    public TMP_Text nameText;
    public Image statusImage;

    void Awake()
    {
        if (nameText) nameText.text = "";
        if (statusImage) statusImage.enabled = false;
    }

    public void Setup(string studentName, Sprite emptySprite)
    {
        if (nameText)
            nameText.text = studentName;

        if (statusImage)
        {
            statusImage.enabled = true;
            statusImage.gameObject.SetActive(true);
            statusImage.sprite = emptySprite;
        }
        SetPresent(false, emptySprite, null);
    }

    public void SetPresent(bool present, Sprite emptySprite, Sprite checkSprite)
    {
        if (!statusImage) return;
        statusImage.sprite = present ? checkSprite : emptySprite;
    }
}
