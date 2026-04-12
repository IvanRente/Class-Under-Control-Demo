using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HistoryTimelineBoard : MonoBehaviour
{
    enum ColumnResultState
    {
        Unchecked,
        Correct,
        Wrong
    }

    class TimelineEventRuntime
    {
        public string id;
        public string label;
        public int columnIndex = -1;
        public int slotIndex = -1;

        public bool IsPlaced => columnIndex >= 0 && slotIndex >= 0;
    }

    [Header("Current Class")]
    public HistoryTimelineClassData currentClassData;

    [Header("Text")]
    public TMP_Text titleText;
    public TMP_Text instructionText;
    [TextArea] public string defaultInstruction = "Select an event, then click an empty slot. Click a placed event to send it back to the bottom.";

    [Header("Pool")]
    public HistoryTimelinePoolEventView[] poolEventViews = new HistoryTimelinePoolEventView[0];

    [Header("Columns")]
    public HistoryTimelineColumnView[] columnViews = new HistoryTimelineColumnView[0];

    [Header("Check Button")]
    public Image checkButtonBackground;
    public TMP_Text checkButtonLabel;
    public GameObject checkButtonDisabledOverlay;
    public HistoryTimelineClickZone checkButtonClickZone;

    [Header("Colors")]
    public Color neutralColumnColor = new Color32(238, 232, 220, 255);
    public Color correctColumnColor = new Color32(124, 204, 132, 255);
    public Color wrongColumnColor = new Color32(224, 106, 106, 255);
    public Color poolEventColor = new Color32(245, 241, 230, 255);
    public Color selectedPoolEventColor = new Color32(255, 219, 120, 255);
    public Color emptySlotColor = new Color32(227, 222, 210, 255);
    public Color filledSlotColor = new Color32(244, 239, 228, 255);
    public Color enabledCheckColor = new Color32(62, 125, 78, 255);
    public Color disabledCheckColor = new Color32(110, 110, 110, 255);

    [Header("GPA")]
    public float gpaPerColumn = 0.5f;

    readonly List<TimelineEventRuntime> activeEvents = new List<TimelineEventRuntime>();
    readonly List<int> pooledEventIndices = new List<int>();
    readonly Dictionary<string, int> eventIndexById = new Dictionary<string, int>();
    readonly HashSet<string> reusableExpectedIds = new HashSet<string>();
    readonly HashSet<string> reusableActualIds = new HashSet<string>();

    GameManager gameManager;
    ColumnResultState[] lastCheckedResults = new ColumnResultState[0];
    int[] slotEventIndices = new int[0];
    int[] poolEventByView = new int[0];
    int selectedEventIndex = -1;
    bool boardChangedSinceLastCheck = true;
    bool hasCheckedAtLeastOnce;
    bool interactionLocked;

    void Awake()
    {
        ResolveReferences();
        ConfigureClickZones();
        InitializeViewState();
    }

    void OnValidate()
    {
        ConfigureClickZones();
    }

    public void LoadClassData(HistoryTimelineClassData classData)
    {
        ResolveReferences();
        ConfigureClickZones();
        currentClassData = classData;
        interactionLocked = false;
        selectedEventIndex = -1;
        boardChangedSinceLastCheck = true;
        hasCheckedAtLeastOnce = false;
        BuildRuntimeState();
        RefreshBoard();
    }

    public void EndClassDisplay()
    {
        interactionLocked = true;
        selectedEventIndex = -1;

        if (titleText != null)
            titleText.text = "class ended";

        UpdateInstructionText();
        RefreshBoard();
    }

    public void HandleClick(HistoryTimelineClickZone.ZoneType zoneType, int poolIndex, int columnIndex, int slotIndex)
    {
        if (interactionLocked)
            return;

        switch (zoneType)
        {
            case HistoryTimelineClickZone.ZoneType.PoolEvent:
                OnPoolEventClicked(poolIndex);
                break;
            case HistoryTimelineClickZone.ZoneType.ColumnSlot:
                OnColumnSlotClicked(columnIndex, slotIndex);
                break;
            case HistoryTimelineClickZone.ZoneType.CheckButton:
                CheckBoard();
                break;
        }
    }

    void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = GameManager.I != null ? GameManager.I : FindObjectOfType<GameManager>();
    }

    void ConfigureClickZones()
    {
        if (poolEventViews != null)
        {
            for (int i = 0; i < poolEventViews.Length; i++)
            {
                HistoryTimelinePoolEventView view = poolEventViews[i];
                if (view == null || view.clickZone == null)
                    continue;

                view.clickZone.board = this;
                view.clickZone.zoneType = HistoryTimelineClickZone.ZoneType.PoolEvent;
                view.clickZone.poolIndex = i;
                view.clickZone.columnIndex = -1;
                view.clickZone.slotIndex = -1;
            }
        }

        if (columnViews != null)
        {
            for (int columnIndex = 0; columnIndex < columnViews.Length; columnIndex++)
            {
                HistoryTimelineColumnView column = columnViews[columnIndex];
                if (column == null || column.slots == null)
                    continue;

                for (int slotIndex = 0; slotIndex < column.slots.Length; slotIndex++)
                {
                    HistoryTimelineSlotView slot = column.slots[slotIndex];
                    if (slot == null || slot.clickZone == null)
                        continue;

                    slot.clickZone.board = this;
                    slot.clickZone.zoneType = HistoryTimelineClickZone.ZoneType.ColumnSlot;
                    slot.clickZone.poolIndex = -1;
                    slot.clickZone.columnIndex = columnIndex;
                    slot.clickZone.slotIndex = slotIndex;
                }
            }
        }

        if (checkButtonClickZone != null)
        {
            checkButtonClickZone.board = this;
            checkButtonClickZone.zoneType = HistoryTimelineClickZone.ZoneType.CheckButton;
            checkButtonClickZone.poolIndex = -1;
            checkButtonClickZone.columnIndex = -1;
            checkButtonClickZone.slotIndex = -1;
        }
    }

    void InitializeViewState()
    {
        BuildRuntimeState();
        RefreshBoard();
    }

    void BuildRuntimeState()
    {
        activeEvents.Clear();
        pooledEventIndices.Clear();
        eventIndexById.Clear();

        int totalSlots = 0;
        if (columnViews != null)
        {
            for (int i = 0; i < columnViews.Length; i++)
            {
                HistoryTimelineColumnView column = columnViews[i];
                if (column != null && column.slots != null)
                    totalSlots += column.slots.Length;
            }
        }

        slotEventIndices = new int[totalSlots];
        for (int i = 0; i < slotEventIndices.Length; i++)
            slotEventIndices[i] = -1;

        poolEventByView = new int[poolEventViews != null ? poolEventViews.Length : 0];
        for (int i = 0; i < poolEventByView.Length; i++)
            poolEventByView[i] = -1;

        lastCheckedResults = new ColumnResultState[columnViews != null ? columnViews.Length : 0];
        for (int i = 0; i < lastCheckedResults.Length; i++)
            lastCheckedResults[i] = ColumnResultState.Unchecked;

        if (currentClassData == null || currentClassData.events == null)
            return;

        for (int i = 0; i < currentClassData.events.Length; i++)
        {
            HistoryTimelineEventData sourceEvent = currentClassData.events[i];
            if (sourceEvent == null)
                continue;

            string eventId = BuildEventId(sourceEvent, i);
            if (string.IsNullOrWhiteSpace(eventId) || eventIndexById.ContainsKey(eventId))
                continue;

            TimelineEventRuntime runtimeEvent = new TimelineEventRuntime
            {
                id = eventId,
                label = string.IsNullOrWhiteSpace(sourceEvent.label) ? eventId : sourceEvent.label
            };

            eventIndexById.Add(runtimeEvent.id, activeEvents.Count);
            activeEvents.Add(runtimeEvent);
            pooledEventIndices.Add(activeEvents.Count - 1);
        }
    }

    string BuildEventId(HistoryTimelineEventData eventData, int index)
    {
        if (eventData == null)
            return string.Empty;

        string rawId = string.IsNullOrWhiteSpace(eventData.eventId) ? eventData.label : eventData.eventId;
        if (string.IsNullOrWhiteSpace(rawId))
            rawId = "event_" + index;

        return rawId.Trim();
    }

    void OnPoolEventClicked(int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= poolEventByView.Length)
            return;

        int eventIndex = poolEventByView[poolIndex];
        if (eventIndex < 0 || eventIndex >= activeEvents.Count)
            return;

        selectedEventIndex = selectedEventIndex == eventIndex ? -1 : eventIndex;
        RefreshBoard();
    }

    void OnColumnSlotClicked(int columnIndex, int slotIndex)
    {
        int flatSlotIndex = GetFlatSlotIndex(columnIndex, slotIndex);
        if (flatSlotIndex < 0 || flatSlotIndex >= slotEventIndices.Length)
            return;

        int existingEventIndex = slotEventIndices[flatSlotIndex];
        if (existingEventIndex >= 0)
        {
            ReturnEventToPool(existingEventIndex);
            selectedEventIndex = -1;
            MarkBoardChanged();
            RefreshBoard();
            return;
        }

        if (selectedEventIndex < 0 || selectedEventIndex >= activeEvents.Count)
            return;

        PlaceSelectedEvent(columnIndex, slotIndex, flatSlotIndex);
        selectedEventIndex = -1;
        MarkBoardChanged();
        RefreshBoard();
    }

    void PlaceSelectedEvent(int columnIndex, int slotIndex, int flatSlotIndex)
    {
        TimelineEventRuntime runtimeEvent = activeEvents[selectedEventIndex];
        runtimeEvent.columnIndex = columnIndex;
        runtimeEvent.slotIndex = slotIndex;
        slotEventIndices[flatSlotIndex] = selectedEventIndex;
        pooledEventIndices.Remove(selectedEventIndex);
    }

    void ReturnEventToPool(int eventIndex)
    {
        TimelineEventRuntime runtimeEvent = activeEvents[eventIndex];
        int flatSlotIndex = GetFlatSlotIndex(runtimeEvent.columnIndex, runtimeEvent.slotIndex);
        if (flatSlotIndex >= 0 && flatSlotIndex < slotEventIndices.Length)
            slotEventIndices[flatSlotIndex] = -1;

        runtimeEvent.columnIndex = -1;
        runtimeEvent.slotIndex = -1;

        if (!pooledEventIndices.Contains(eventIndex))
        {
            pooledEventIndices.Add(eventIndex);
            pooledEventIndices.Sort();
        }
    }

    void MarkBoardChanged()
    {
        boardChangedSinceLastCheck = true;

        for (int i = 0; i < lastCheckedResults.Length; i++)
            ApplyColumnResultColor(i, ColumnResultState.Unchecked);
    }

    void CheckBoard()
    {
        if (!CanCheckBoard())
            return;

        ColumnResultState[] currentResults = EvaluateColumns();
        ApplyGpaDelta(currentResults);

        for (int i = 0; i < currentResults.Length; i++)
            lastCheckedResults[i] = currentResults[i];

        hasCheckedAtLeastOnce = true;
        boardChangedSinceLastCheck = false;
        selectedEventIndex = -1;
        RefreshBoard();
    }

    ColumnResultState[] EvaluateColumns()
    {
        ColumnResultState[] results = new ColumnResultState[columnViews != null ? columnViews.Length : 0];

        for (int columnIndex = 0; columnIndex < results.Length; columnIndex++)
        {
            bool correct = IsColumnCorrect(columnIndex);
            results[columnIndex] = correct ? ColumnResultState.Correct : ColumnResultState.Wrong;
            ApplyColumnResultColor(columnIndex, results[columnIndex]);
        }

        return results;
    }

    bool IsColumnCorrect(int columnIndex)
    {
        HistoryTimelineColumnData[] configuredColumns = currentClassData != null ? currentClassData.columns : null;
        if (configuredColumns == null || columnIndex < 0 || columnIndex >= configuredColumns.Length)
            return false;

        HistoryTimelineColumnData columnData = configuredColumns[columnIndex];
        if (columnData == null)
            return false;

        reusableExpectedIds.Clear();
        reusableActualIds.Clear();

        string[] expectedEventIds = columnData.expectedEventIds;
        if (expectedEventIds != null)
        {
            for (int i = 0; i < expectedEventIds.Length; i++)
            {
                string expectedId = expectedEventIds[i];
                if (!string.IsNullOrWhiteSpace(expectedId))
                    reusableExpectedIds.Add(expectedId.Trim());
            }
        }

        HistoryTimelineColumnView columnView = columnViews != null && columnIndex < columnViews.Length
            ? columnViews[columnIndex]
            : null;

        int filledSlotCount = 0;
        int slotCount = columnView != null && columnView.slots != null ? columnView.slots.Length : 0;
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            int eventIndex = GetEventIndexAt(columnIndex, slotIndex);
            if (eventIndex < 0 || eventIndex >= activeEvents.Count)
                continue;

            filledSlotCount++;
            reusableActualIds.Add(activeEvents[eventIndex].id);
        }

        if (filledSlotCount != reusableExpectedIds.Count)
            return false;

        if (reusableActualIds.Count != reusableExpectedIds.Count)
            return false;

        foreach (string actualId in reusableActualIds)
        {
            if (!reusableExpectedIds.Contains(actualId))
                return false;
        }

        return true;
    }

    void ApplyGpaDelta(ColumnResultState[] currentResults)
    {
        if (gameManager == null)
            return;

        float delta = 0f;
        for (int i = 0; i < currentResults.Length; i++)
        {
            ColumnResultState current = currentResults[i];
            ColumnResultState previous = hasCheckedAtLeastOnce && i < lastCheckedResults.Length
                ? lastCheckedResults[i]
                : ColumnResultState.Unchecked;

            if (!hasCheckedAtLeastOnce)
            {
                if (current == ColumnResultState.Correct)
                    delta += gpaPerColumn;
                else if (current == ColumnResultState.Wrong)
                    delta -= gpaPerColumn;

                continue;
            }

            if (previous == current)
                continue;

            if (current == ColumnResultState.Correct)
                delta += gpaPerColumn;
            else if (current == ColumnResultState.Wrong)
                delta -= gpaPerColumn;
        }

        if (delta > 0f)
            gameManager.AddGPA(delta);
        else if (delta < 0f)
            gameManager.SubGPA(-delta);
    }

    bool CanCheckBoard()
    {
        return !interactionLocked && boardChangedSinceLastCheck && AreAllEventsPlaced();
    }

    bool AreAllEventsPlaced()
    {
        return activeEvents.Count > 0 && pooledEventIndices.Count == 0;
    }

    int GetFlatSlotIndex(int columnIndex, int slotIndex)
    {
        if (columnViews == null || columnIndex < 0 || columnIndex >= columnViews.Length)
            return -1;

        int offset = 0;
        for (int i = 0; i < columnIndex; i++)
        {
            HistoryTimelineColumnView column = columnViews[i];
            if (column != null && column.slots != null)
                offset += column.slots.Length;
        }

        HistoryTimelineColumnView targetColumn = columnViews[columnIndex];
        if (targetColumn == null || targetColumn.slots == null || slotIndex < 0 || slotIndex >= targetColumn.slots.Length)
            return -1;

        return offset + slotIndex;
    }

    int GetEventIndexAt(int columnIndex, int slotIndex)
    {
        int flatSlotIndex = GetFlatSlotIndex(columnIndex, slotIndex);
        if (flatSlotIndex < 0 || flatSlotIndex >= slotEventIndices.Length)
            return -1;

        return slotEventIndices[flatSlotIndex];
    }

    void RefreshBoard()
    {
        RefreshTitle();
        RefreshColumns();
        RefreshPool();
        RefreshCheckButton();
        UpdateInstructionText();
    }

    void RefreshTitle()
    {
        if (titleText == null)
            return;

        if (interactionLocked)
        {
            titleText.text = "class ended";
            return;
        }

        if (currentClassData != null && !string.IsNullOrWhiteSpace(currentClassData.title))
        {
            titleText.text = currentClassData.title;
            return;
        }

        titleText.text = "History Timeline";
    }

    void RefreshColumns()
    {
        if (columnViews == null)
            return;

        HistoryTimelineColumnData[] configuredColumns = currentClassData != null ? currentClassData.columns : null;

        for (int columnIndex = 0; columnIndex < columnViews.Length; columnIndex++)
        {
            HistoryTimelineColumnView columnView = columnViews[columnIndex];
            if (columnView == null)
                continue;

            if (columnView.rangeLabel != null)
            {
                string label = configuredColumns != null && columnIndex < configuredColumns.Length && configuredColumns[columnIndex] != null
                    ? configuredColumns[columnIndex].rangeLabel
                    : string.Empty;
                columnView.rangeLabel.text = label;
            }

            ApplyColumnResultColor(columnIndex, boardChangedSinceLastCheck ? ColumnResultState.Unchecked : SafeGetCheckedResult(columnIndex));

            if (columnView.slots == null)
                continue;

            for (int slotIndex = 0; slotIndex < columnView.slots.Length; slotIndex++)
                RefreshSlot(columnIndex, slotIndex, columnView.slots[slotIndex]);
        }
    }

    void RefreshSlot(int columnIndex, int slotIndex, HistoryTimelineSlotView slotView)
    {
        if (slotView == null)
            return;

        int eventIndex = GetEventIndexAt(columnIndex, slotIndex);
        bool hasEvent = eventIndex >= 0 && eventIndex < activeEvents.Count;

        if (slotView.root != null)
            slotView.root.SetActive(true);

        if (slotView.label != null)
            slotView.label.text = hasEvent ? activeEvents[eventIndex].label : string.Empty;

        if (slotView.background != null)
            slotView.background.color = hasEvent ? filledSlotColor : emptySlotColor;

        if (slotView.selectionHighlight != null)
            slotView.selectionHighlight.SetActive(false);
    }

    void RefreshPool()
    {
        if (poolEventViews == null)
            return;

        for (int i = 0; i < poolEventByView.Length; i++)
            poolEventByView[i] = i < pooledEventIndices.Count ? pooledEventIndices[i] : -1;

        for (int i = 0; i < poolEventViews.Length; i++)
        {
            HistoryTimelinePoolEventView view = poolEventViews[i];
            if (view == null)
                continue;

            int eventIndex = i < poolEventByView.Length ? poolEventByView[i] : -1;
            bool visible = eventIndex >= 0 && eventIndex < activeEvents.Count;

            if (view.root != null)
                view.root.SetActive(visible);

            if (!visible)
                continue;

            if (view.label != null)
                view.label.text = activeEvents[eventIndex].label;

            bool selected = selectedEventIndex == eventIndex;
            if (view.background != null)
                view.background.color = selected ? selectedPoolEventColor : poolEventColor;

            if (view.selectionHighlight != null)
                view.selectionHighlight.SetActive(selected);
        }
    }

    void RefreshCheckButton()
    {
        bool canCheck = CanCheckBoard();

        if (checkButtonBackground != null)
            checkButtonBackground.color = canCheck ? enabledCheckColor : disabledCheckColor;

        if (checkButtonDisabledOverlay != null)
            checkButtonDisabledOverlay.SetActive(!canCheck);

        if (checkButtonLabel != null)
            checkButtonLabel.text = "Check";
    }

    void UpdateInstructionText()
    {
        if (instructionText == null)
            return;

        if (interactionLocked)
        {
            instructionText.text = string.Empty;
            return;
        }

        if (selectedEventIndex >= 0 && selectedEventIndex < activeEvents.Count)
        {
            instructionText.text = "Selected: " + activeEvents[selectedEventIndex].label + "\nClick an empty slot to place it.";
            return;
        }

        if (!AreAllEventsPlaced())
        {
            instructionText.text = defaultInstruction;
            return;
        }

        instructionText.text = boardChangedSinceLastCheck
            ? "All events are placed. Click Check to evaluate the board."
            : "Move at least one event before checking again.";
    }

    ColumnResultState SafeGetCheckedResult(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= lastCheckedResults.Length)
            return ColumnResultState.Unchecked;

        return lastCheckedResults[columnIndex];
    }

    void ApplyColumnResultColor(int columnIndex, ColumnResultState result)
    {
        if (columnViews == null || columnIndex < 0 || columnIndex >= columnViews.Length)
            return;

        HistoryTimelineColumnView columnView = columnViews[columnIndex];
        if (columnView == null || columnView.background == null)
            return;

        switch (result)
        {
            case ColumnResultState.Correct:
                columnView.background.color = correctColumnColor;
                break;
            case ColumnResultState.Wrong:
                columnView.background.color = wrongColumnColor;
                break;
            default:
                columnView.background.color = neutralColumnColor;
                break;
        }
    }
}
