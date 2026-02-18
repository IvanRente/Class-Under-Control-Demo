using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RollCallRowUI : MonoBehaviour
{
    public TMP_Text nameText;
    public Image statusImage;

    public void Setup(string studentName, Sprite emptySprite)
    {
        if (nameText) nameText.text = studentName;
        SetPresent(false, emptySprite, null);
    }

    public void SetPresent(bool present, Sprite emptySprite, Sprite checkSprite)
    {
        if (!statusImage) return;
        statusImage.sprite = present ? checkSprite : emptySprite;
    }
}
