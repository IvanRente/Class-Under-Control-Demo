using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CircuitBuilderBoard : MonoBehaviour
{
    class CircuitComponentRuntime
    {
        public CircuitComponentType componentType;
        public string label;
        public Sprite icon;
        public int socketIndex = -1;

        public bool IsPlaced => socketIndex >= 0;
    }

    [Header("Current Class")]
    public CircuitBuilderClassData currentClassData;

    [Header("Text")]
    public TMP_Text titleText;
    public TMP_Text instructionText;
    public TMP_Text progressText;

    [Header("Circuit Views")]
    public CircuitPuzzleView[] puzzleViews = new CircuitPuzzleView[0];

    [Header("Component Cards")]
    public CircuitComponentCardView[] componentCardViews = new CircuitComponentCardView[0];

    [Header("Check Button")]
    public Image checkButtonBackground;
    public TMP_Text checkButtonLabel;
    public GameObject checkButtonDisabledOverlay;
    public CircuitBuilderClickZone checkButtonClickZone;

    [Header("Colors")]
    public Color emptySocketColor = new Color32(227, 222, 210, 255);
    public Color filledSocketColor = new Color32(244, 239, 228, 255);
    public Color correctSocketColor = new Color32(124, 204, 132, 255);
    public Color wrongSocketColor = new Color32(224, 106, 106, 255);
    public Color cardColor = new Color32(245, 241, 230, 255);
    public Color selectedCardColor = new Color32(255, 219, 120, 255);
    public Color enabledCheckColor = new Color32(62, 125, 78, 255);
    public Color disabledCheckColor = new Color32(110, 110, 110, 255);
    public Color electricGlowColor = new Color32(47, 255, 122, 180);
    public Color electricGlowPulseColor = new Color32(130, 255, 176, 255);
    public float glowPulseSpeed = 4f;

    [Header("Progress")]
    public float solvedHoldSeconds = 1.25f;
    public float gpaGainPerSolvedCircuit = 0.5f;
    public float gpaPenaltyPerWrongCheck = 0.2f;

    readonly List<CircuitComponentRuntime> activeComponents = new List<CircuitComponentRuntime>();

    GameManager gameManager;
    bool[] solvedCircuits = new bool[0];
    int[] socketComponentIndices = new int[0];
    int[] cardComponentByView = new int[0];
    int currentCircuitIndex;
    int selectedComponentIndex = -1;
    bool boardChangedSinceLastCheck = true;
    bool interactionLocked;
    bool advancingToNextCircuit;
    bool classEndedDisplay;
    Coroutine advanceCoroutine;

    void Awake()
    {
        ResolveReferences();
        ConfigureClickZones();
        InitializeViewState();
    }

    void Update()
    {
        UpdateGlowPulse();
    }

    void OnValidate()
    {
        ConfigureClickZones();
    }

    public void LoadClassData(CircuitBuilderClassData classData)
    {
        ResolveReferences();
        ConfigureClickZones();
        currentClassData = classData;
        currentCircuitIndex = 0;
        selectedComponentIndex = -1;
        interactionLocked = false;
        advancingToNextCircuit = false;
        classEndedDisplay = false;
        boardChangedSinceLastCheck = true;
        solvedCircuits = new bool[GetCircuitCount()];
        BuildCurrentCircuitState();
        RefreshBoard();
    }

    public void EndClassDisplay()
    {
        classEndedDisplay = true;
        interactionLocked = true;
        selectedComponentIndex = -1;

        if (advanceCoroutine != null)
        {
            StopCoroutine(advanceCoroutine);
            advanceCoroutine = null;
        }

        if (titleText != null)
            titleText.text = "class ended";

        RefreshBoard();
    }

    public void HandleClick(CircuitBuilderClickZone.ZoneType zoneType, int cardIndex, int puzzleIndex, int socketIndex)
    {
        if (interactionLocked || advancingToNextCircuit)
            return;

        switch (zoneType)
        {
            case CircuitBuilderClickZone.ZoneType.ComponentCard:
                OnComponentCardClicked(cardIndex);
                break;
            case CircuitBuilderClickZone.ZoneType.Socket:
                OnSocketClicked(puzzleIndex, socketIndex);
                break;
            case CircuitBuilderClickZone.ZoneType.CheckButton:
                CheckCurrentCircuit();
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
        if (componentCardViews != null)
        {
            for (int i = 0; i < componentCardViews.Length; i++)
            {
                CircuitComponentCardView view = componentCardViews[i];
                if (view == null || view.clickZone == null)
                    continue;

                view.clickZone.board = this;
                view.clickZone.zoneType = CircuitBuilderClickZone.ZoneType.ComponentCard;
                view.clickZone.cardIndex = i;
                view.clickZone.puzzleIndex = -1;
                view.clickZone.socketIndex = -1;
            }
        }

        if (puzzleViews != null)
        {
            for (int puzzleIndex = 0; puzzleIndex < puzzleViews.Length; puzzleIndex++)
            {
                CircuitPuzzleView puzzleView = puzzleViews[puzzleIndex];
                if (puzzleView == null || puzzleView.socketViews == null)
                    continue;

                for (int socketIndex = 0; socketIndex < puzzleView.socketViews.Length; socketIndex++)
                {
                    CircuitSocketView socketView = puzzleView.socketViews[socketIndex];
                    if (socketView == null || socketView.clickZone == null)
                        continue;

                    socketView.clickZone.board = this;
                    socketView.clickZone.zoneType = CircuitBuilderClickZone.ZoneType.Socket;
                    socketView.clickZone.cardIndex = -1;
                    socketView.clickZone.puzzleIndex = puzzleIndex;
                    socketView.clickZone.socketIndex = socketIndex;
                }
            }
        }

        if (checkButtonClickZone != null)
        {
            checkButtonClickZone.board = this;
            checkButtonClickZone.zoneType = CircuitBuilderClickZone.ZoneType.CheckButton;
            checkButtonClickZone.cardIndex = -1;
            checkButtonClickZone.puzzleIndex = -1;
            checkButtonClickZone.socketIndex = -1;
        }
    }

    void InitializeViewState()
    {
        solvedCircuits = new bool[GetCircuitCount()];
        BuildCurrentCircuitState();
        RefreshBoard();
    }

    void BuildCurrentCircuitState()
    {
        activeComponents.Clear();
        selectedComponentIndex = -1;
        boardChangedSinceLastCheck = true;
        interactionLocked = IsAllCircuitsComplete();
        advancingToNextCircuit = false;

        CircuitPuzzleData puzzleData = GetCurrentPuzzleData();
        CircuitPuzzleView puzzleView = GetCurrentPuzzleView();
        int socketCount = puzzleData != null && puzzleData.sockets != null ? puzzleData.sockets.Length : 0;
        int viewSocketCount = puzzleView != null && puzzleView.socketViews != null ? puzzleView.socketViews.Length : 0;
        socketCount = Mathf.Min(socketCount, viewSocketCount);

        socketComponentIndices = new int[socketCount];
        for (int i = 0; i < socketComponentIndices.Length; i++)
            socketComponentIndices[i] = -1;

        cardComponentByView = new int[componentCardViews != null ? componentCardViews.Length : 0];
        for (int i = 0; i < cardComponentByView.Length; i++)
            cardComponentByView[i] = -1;

        if (puzzleData != null && puzzleData.components != null)
        {
            for (int i = 0; i < puzzleData.components.Length; i++)
            {
                CircuitComponentData componentData = puzzleData.components[i];
                if (componentData == null)
                    continue;

                CircuitComponentRuntime runtimeComponent = new CircuitComponentRuntime
                {
                    componentType = componentData.componentType,
                    label = string.IsNullOrWhiteSpace(componentData.label) ? componentData.componentType.ToString() : componentData.label,
                    icon = componentData.icon
                };

                activeComponents.Add(runtimeComponent);
            }
        }

        for (int i = 0; i < cardComponentByView.Length && i < activeComponents.Count; i++)
            cardComponentByView[i] = i;
    }

    void OnComponentCardClicked(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= cardComponentByView.Length)
            return;

        int componentIndex = cardComponentByView[cardIndex];
        if (componentIndex < 0 || componentIndex >= activeComponents.Count || activeComponents[componentIndex].IsPlaced)
            return;

        selectedComponentIndex = selectedComponentIndex == componentIndex ? -1 : componentIndex;
        RefreshBoard();
    }

    void OnSocketClicked(int puzzleIndex, int socketIndex)
    {
        if (puzzleIndex != currentCircuitIndex || socketIndex < 0 || socketIndex >= socketComponentIndices.Length)
            return;

        int existingComponentIndex = socketComponentIndices[socketIndex];
        if (existingComponentIndex >= 0)
        {
            ReturnComponentToCards(existingComponentIndex);
            selectedComponentIndex = -1;
            MarkBoardChanged();
            RefreshBoard();
            return;
        }

        if (selectedComponentIndex < 0 || selectedComponentIndex >= activeComponents.Count)
            return;

        PlaceSelectedComponent(socketIndex);
        selectedComponentIndex = -1;
        MarkBoardChanged();
        RefreshBoard();
    }

    void PlaceSelectedComponent(int socketIndex)
    {
        CircuitComponentRuntime runtimeComponent = activeComponents[selectedComponentIndex];
        runtimeComponent.socketIndex = socketIndex;
        socketComponentIndices[socketIndex] = selectedComponentIndex;
    }

    void ReturnComponentToCards(int componentIndex)
    {
        if (componentIndex < 0 || componentIndex >= activeComponents.Count)
            return;

        CircuitComponentRuntime runtimeComponent = activeComponents[componentIndex];
        if (runtimeComponent.socketIndex >= 0 && runtimeComponent.socketIndex < socketComponentIndices.Length)
            socketComponentIndices[runtimeComponent.socketIndex] = -1;

        runtimeComponent.socketIndex = -1;
    }

    void MarkBoardChanged()
    {
        boardChangedSinceLastCheck = true;
    }

    void CheckCurrentCircuit()
    {
        if (!CanCheckCircuit())
            return;

        if (IsCircuitValid())
        {
            SolveCurrentCircuit();
            return;
        }

        boardChangedSinceLastCheck = false;
        selectedComponentIndex = -1;

        if (gameManager != null && gpaPenaltyPerWrongCheck > 0f)
            gameManager.SubGPA(gpaPenaltyPerWrongCheck);

        RefreshBoard();
    }

    void SolveCurrentCircuit()
    {
        if (currentCircuitIndex >= 0 && currentCircuitIndex < solvedCircuits.Length && !solvedCircuits[currentCircuitIndex])
        {
            solvedCircuits[currentCircuitIndex] = true;
            if (gameManager != null && gpaGainPerSolvedCircuit > 0f)
                gameManager.AddGPA(gpaGainPerSolvedCircuit);
        }

        boardChangedSinceLastCheck = false;
        selectedComponentIndex = -1;
        interactionLocked = true;
        advancingToNextCircuit = true;
        RefreshBoard();

        if (currentCircuitIndex + 1 < GetCircuitCount())
        {
            advanceCoroutine = StartCoroutine(AdvanceToNextCircuitAfterDelay());
            return;
        }

        advancingToNextCircuit = false;
        RefreshBoard();
    }

    IEnumerator AdvanceToNextCircuitAfterDelay()
    {
        if (solvedHoldSeconds > 0f)
            yield return new WaitForSeconds(solvedHoldSeconds);

        currentCircuitIndex++;
        interactionLocked = false;
        advancingToNextCircuit = false;
        advanceCoroutine = null;
        BuildCurrentCircuitState();
        RefreshBoard();
    }

    bool CanCheckCircuit()
    {
        return !interactionLocked && !advancingToNextCircuit && boardChangedSinceLastCheck && AreAllSocketsFilled();
    }

    bool AreAllSocketsFilled()
    {
        if (socketComponentIndices == null || socketComponentIndices.Length == 0)
            return false;

        for (int i = 0; i < socketComponentIndices.Length; i++)
        {
            if (socketComponentIndices[i] < 0)
                return false;
        }

        return true;
    }

    bool IsCircuitValid()
    {
        for (int i = 0; i < socketComponentIndices.Length; i++)
        {
            if (!IsSocketCorrect(i))
                return false;
        }

        return socketComponentIndices.Length > 0;
    }

    bool IsSocketCorrect(int socketIndex)
    {
        CircuitPuzzleData puzzleData = GetCurrentPuzzleData();
        if (puzzleData == null || puzzleData.sockets == null || socketIndex < 0 || socketIndex >= puzzleData.sockets.Length)
            return false;

        int componentIndex = socketComponentIndices != null && socketIndex < socketComponentIndices.Length
            ? socketComponentIndices[socketIndex]
            : -1;

        if (componentIndex < 0 || componentIndex >= activeComponents.Count)
            return false;

        return activeComponents[componentIndex].componentType == puzzleData.sockets[socketIndex].expectedComponent;
    }

    void RefreshBoard()
    {
        RefreshText();
        RefreshPuzzleViews();
        RefreshCards();
        RefreshCheckButton();
        RefreshElectricFlow();
    }

    void RefreshText()
    {
        CircuitPuzzleData puzzleData = GetCurrentPuzzleData();

        if (titleText != null)
        {
            if (classEndedDisplay)
                titleText.text = "class ended";
            else if (interactionLocked && IsAllCircuitsComplete())
                titleText.text = "All physics circuits complete!";
            else if (interactionLocked && advancingToNextCircuit)
                titleText.text = "Circuit complete!";
            else if (puzzleData != null && !string.IsNullOrWhiteSpace(puzzleData.title))
                titleText.text = puzzleData.title;
            else
                titleText.text = "Circuit Builder";
        }

        if (instructionText != null)
        {
            if (classEndedDisplay)
                instructionText.text = string.Empty;
            else if (interactionLocked && IsAllCircuitsComplete())
                instructionText.text = "Every circuit is valid. The bulbs are lit.";
            else if (interactionLocked && advancingToNextCircuit)
                instructionText.text = "Current is flowing. Loading the next circuit...";
            else if (selectedComponentIndex >= 0 && selectedComponentIndex < activeComponents.Count)
                instructionText.text = "Selected: " + activeComponents[selectedComponentIndex].label + "\nClick an empty socket to place it.";
            else if (!boardChangedSinceLastCheck && !IsCircuitValid())
                instructionText.text = "That circuit is not valid. Move at least one component before checking again.";
            else if (puzzleData != null && !string.IsNullOrWhiteSpace(puzzleData.instruction))
                instructionText.text = puzzleData.instruction;
            else
                instructionText.text = "Select a component, then click an empty socket.";
        }

        if (progressText != null)
            progressText.text = GetCircuitCount() > 0 ? $"Circuit {currentCircuitIndex + 1}/{GetCircuitCount()}" : "No circuits";
    }

    void RefreshPuzzleViews()
    {
        if (puzzleViews == null)
            return;

        for (int puzzleIndex = 0; puzzleIndex < puzzleViews.Length; puzzleIndex++)
        {
            CircuitPuzzleView puzzleView = puzzleViews[puzzleIndex];
            if (puzzleView == null)
                continue;

            bool active = puzzleIndex == currentCircuitIndex && puzzleIndex < GetCircuitCount();
            if (puzzleView.root != null)
                puzzleView.root.SetActive(active);

            if (!active)
                continue;

            RefreshSocketViews(puzzleView);

            if (puzzleView.solvedEffectRoot != null)
                puzzleView.solvedEffectRoot.SetActive(interactionLocked && IsCircuitValid());
        }
    }

    void RefreshSocketViews(CircuitPuzzleView puzzleView)
    {
        CircuitPuzzleData puzzleData = GetCurrentPuzzleData();
        if (puzzleView == null || puzzleView.socketViews == null)
            return;

        for (int socketIndex = 0; socketIndex < puzzleView.socketViews.Length; socketIndex++)
        {
            CircuitSocketView socketView = puzzleView.socketViews[socketIndex];
            if (socketView == null)
                continue;

            bool hasData = puzzleData != null && puzzleData.sockets != null && socketIndex < puzzleData.sockets.Length;
            if (socketView.root != null)
                socketView.root.SetActive(hasData);

            if (!hasData)
                continue;

            if (socketView.socketLabel != null)
                socketView.socketLabel.text = puzzleData.sockets[socketIndex].socketLabel;

            int componentIndex = socketIndex < socketComponentIndices.Length ? socketComponentIndices[socketIndex] : -1;
            bool hasComponent = componentIndex >= 0 && componentIndex < activeComponents.Count;

            if (socketView.componentLabel != null)
                socketView.componentLabel.text = hasComponent ? activeComponents[componentIndex].label : string.Empty;

            if (socketView.componentIcon != null)
            {
                socketView.componentIcon.sprite = hasComponent ? activeComponents[componentIndex].icon : null;
                socketView.componentIcon.enabled = hasComponent && activeComponents[componentIndex].icon != null;
            }

            if (socketView.background != null)
                socketView.background.color = GetSocketColor(socketIndex, hasComponent);
        }
    }

    Color GetSocketColor(int socketIndex, bool hasComponent)
    {
        if (interactionLocked && IsSocketCorrect(socketIndex))
            return correctSocketColor;

        if (!boardChangedSinceLastCheck && hasComponent)
            return IsSocketCorrect(socketIndex) ? correctSocketColor : wrongSocketColor;

        return hasComponent ? filledSocketColor : emptySocketColor;
    }

    void RefreshCards()
    {
        if (componentCardViews == null)
            return;

        for (int i = 0; i < componentCardViews.Length; i++)
        {
            CircuitComponentCardView cardView = componentCardViews[i];
            if (cardView == null)
                continue;

            int componentIndex = i < cardComponentByView.Length ? cardComponentByView[i] : -1;
            bool visible = !interactionLocked && !advancingToNextCircuit && componentIndex >= 0 && componentIndex < activeComponents.Count && !activeComponents[componentIndex].IsPlaced && !IsAllCircuitsComplete();

            if (cardView.root != null)
                cardView.root.SetActive(visible);

            if (!visible)
                continue;

            CircuitComponentRuntime component = activeComponents[componentIndex];
            if (cardView.label != null)
                cardView.label.text = component.label;

            if (cardView.icon != null)
            {
                cardView.icon.sprite = component.icon;
                cardView.icon.enabled = component.icon != null;
            }

            bool selected = selectedComponentIndex == componentIndex;
            if (cardView.background != null)
                cardView.background.color = selected ? selectedCardColor : cardColor;

            if (cardView.selectionHighlight != null)
                cardView.selectionHighlight.SetActive(selected);
        }
    }

    void RefreshCheckButton()
    {
        bool canCheck = CanCheckCircuit();

        if (checkButtonBackground != null)
            checkButtonBackground.color = canCheck ? enabledCheckColor : disabledCheckColor;

        if (checkButtonDisabledOverlay != null)
            checkButtonDisabledOverlay.SetActive(!canCheck);

        if (checkButtonLabel != null)
            checkButtonLabel.text = IsAllCircuitsComplete() ? "Done" : "Check";
    }

    void RefreshElectricFlow()
    {
        CircuitPuzzleView puzzleView = GetCurrentPuzzleView();
        if (puzzleView == null)
            return;

        bool flowActive = true;
        int socketCount = puzzleView.socketViews != null ? puzzleView.socketViews.Length : 0;

        for (int socketIndex = 0; socketIndex < socketCount; socketIndex++)
        {
            bool socketCorrect = socketIndex < socketComponentIndices.Length && IsSocketCorrect(socketIndex);
            bool socketHasFlow = flowActive && socketCorrect;
            SetSocketGlow(puzzleView.socketViews[socketIndex], socketHasFlow);
            flowActive = socketHasFlow;

            if (puzzleView.pathSegments != null && socketIndex < puzzleView.pathSegments.Length)
                SetPathGlow(puzzleView.pathSegments[socketIndex], flowActive);
        }

        if (puzzleView.pathSegments == null)
            return;

        for (int i = socketCount; i < puzzleView.pathSegments.Length; i++)
            SetPathGlow(puzzleView.pathSegments[i], false);
    }

    void SetSocketGlow(CircuitSocketView socketView, bool active)
    {
        if (socketView == null || socketView.flowGlowRoot == null)
            return;

        socketView.flowGlowRoot.SetActive(active);
    }

    void SetPathGlow(CircuitPathSegmentView segmentView, bool active)
    {
        if (segmentView == null || segmentView.flowGlowRoot == null)
            return;

        segmentView.flowGlowRoot.SetActive(active);
    }

    void UpdateGlowPulse()
    {
        CircuitPuzzleView puzzleView = GetCurrentPuzzleView();
        if (puzzleView == null)
            return;

        float pulse = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
        Color glowColor = Color.Lerp(electricGlowColor, electricGlowPulseColor, pulse);

        if (puzzleView.socketViews != null)
        {
            for (int i = 0; i < puzzleView.socketViews.Length; i++)
            {
                CircuitSocketView socketView = puzzleView.socketViews[i];
                if (socketView != null && socketView.flowGlowImage != null && socketView.flowGlowImage.gameObject.activeInHierarchy)
                    socketView.flowGlowImage.color = glowColor;
            }
        }

        if (puzzleView.pathSegments == null)
            return;

        for (int i = 0; i < puzzleView.pathSegments.Length; i++)
        {
            CircuitPathSegmentView segmentView = puzzleView.pathSegments[i];
            if (segmentView != null && segmentView.flowGlowImage != null && segmentView.flowGlowImage.gameObject.activeInHierarchy)
                segmentView.flowGlowImage.color = glowColor;
        }
    }

    CircuitPuzzleData GetCurrentPuzzleData()
    {
        if (currentClassData == null || currentClassData.circuits == null)
            return null;

        if (currentCircuitIndex < 0 || currentCircuitIndex >= currentClassData.circuits.Length)
            return null;

        return currentClassData.circuits[currentCircuitIndex];
    }

    CircuitPuzzleView GetCurrentPuzzleView()
    {
        if (puzzleViews == null || currentCircuitIndex < 0 || currentCircuitIndex >= puzzleViews.Length)
            return null;

        return puzzleViews[currentCircuitIndex];
    }

    int GetCircuitCount()
    {
        if (currentClassData == null || currentClassData.circuits == null)
            return 0;

        return Mathf.Min(currentClassData.circuits.Length, puzzleViews != null ? puzzleViews.Length : 0);
    }

    bool IsAllCircuitsComplete()
    {
        int circuitCount = GetCircuitCount();
        if (circuitCount <= 0 || solvedCircuits == null || solvedCircuits.Length < circuitCount)
            return false;

        for (int i = 0; i < circuitCount; i++)
        {
            if (!solvedCircuits[i])
                return false;
        }

        return true;
    }
}
