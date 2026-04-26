using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class CircuitComponentCardView
{
    public GameObject root;
    public TMP_Text label;
    public Image icon;
    public Image background;
    public GameObject selectionHighlight;
    public CircuitBuilderClickZone clickZone;
}

[System.Serializable]
public class CircuitSocketView
{
    public GameObject root;
    public TMP_Text socketLabel;
    public TMP_Text componentLabel;
    public Image componentIcon;
    public Image background;
    public GameObject flowGlowRoot;
    public Image flowGlowImage;
    public CircuitBuilderClickZone clickZone;
}

[System.Serializable]
public class CircuitPathSegmentView
{
    public GameObject root;
    public GameObject flowGlowRoot;
    public Image flowGlowImage;
}

[System.Serializable]
public class CircuitPuzzleView
{
    public GameObject root;
    public CircuitSocketView[] socketViews = new CircuitSocketView[0];
    public CircuitPathSegmentView[] pathSegments = new CircuitPathSegmentView[0];
    public GameObject solvedEffectRoot;
}
