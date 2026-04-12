using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class HistoryTimelinePoolEventView
{
    public GameObject root;
    public TMP_Text label;
    public Image background;
    public GameObject selectionHighlight;
    public HistoryTimelineClickZone clickZone;
}

[System.Serializable]
public class HistoryTimelineSlotView
{
    public GameObject root;
    public TMP_Text label;
    public Image background;
    public GameObject selectionHighlight;
    public HistoryTimelineClickZone clickZone;
}

[System.Serializable]
public class HistoryTimelineColumnView
{
    public TMP_Text rangeLabel;
    public Image background;
    public HistoryTimelineSlotView[] slots = new HistoryTimelineSlotView[0];
}
