using UnityEngine;

public class HistoryTimelineClickZone : MonoBehaviour, IPrimaryClickReceiver
{
    public enum ZoneType
    {
        PoolEvent,
        ColumnSlot,
        CheckButton
    }

    public HistoryTimelineBoard board;
    public ZoneType zoneType;
    public int poolIndex = -1;
    public int columnIndex = -1;
    public int slotIndex = -1;

    public void OnPrimaryClick(PlayerController player)
    {
        if (board == null)
            board = GetComponentInParent<HistoryTimelineBoard>();

        if (board == null)
            return;

        board.HandleClick(zoneType, poolIndex, columnIndex, slotIndex);
    }
}
