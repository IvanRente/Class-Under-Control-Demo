using System;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp", sourceClassName: "PlayerItemSystem+ItemButtonBinding")]
[Serializable]
public class PlayerItemButtonBinding
{
    public PlayerItemSystem.ItemId itemId;
    public Button button;
    public TMP_Text label;
    public Image background;
    public Slider durabilityBar;
    public Image durabilityFill;

    public void ResolveReferences()
    {
        if (button == null)
            return;

        if (background == null)
            background = button.targetGraphic as Image ?? button.GetComponent<Image>();

        if (label == null)
            label = button.GetComponentInChildren<TMP_Text>(true);

        if (durabilityBar == null)
            durabilityBar = button.GetComponentInChildren<Slider>(true);

        if (durabilityFill == null && durabilityBar != null && durabilityBar.fillRect != null)
            durabilityFill = durabilityBar.fillRect.GetComponent<Image>();
    }
}
