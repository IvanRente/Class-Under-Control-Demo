using UnityEngine;

public class CircuitBuilderClickZone : MonoBehaviour, IPrimaryClickReceiver
{
    public enum ZoneType
    {
        ComponentCard,
        Socket,
        CheckButton
    }

    public CircuitBuilderBoard board;
    public ZoneType zoneType;
    public int cardIndex = -1;
    public int puzzleIndex = -1;
    public int socketIndex = -1;

    public void OnPrimaryClick(PlayerController player)
    {
        if (board == null)
            board = GetComponentInParent<CircuitBuilderBoard>();

        if (board == null)
            return;

        board.HandleClick(zoneType, cardIndex, puzzleIndex, socketIndex);
    }
}
