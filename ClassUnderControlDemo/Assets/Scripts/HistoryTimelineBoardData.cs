using UnityEngine;

public enum ClassBoardType
{
    Quiz,
    HistoryTimeline,
    CircuitBuilder
}

[System.Serializable]
public class HistoryTimelineEventData
{
    public string eventId = "event_id";
    [TextArea] public string label;
}

[System.Serializable]
public class HistoryTimelineColumnData
{
    public string rangeLabel = "Time Range";
    public string[] expectedEventIds = new string[0];
}

[System.Serializable]
public class HistoryTimelineClassData
{
    public string title = "Place each event in the correct time range";
    public HistoryTimelineColumnData[] columns = new HistoryTimelineColumnData[0];
    public HistoryTimelineEventData[] events = new HistoryTimelineEventData[0];
}
