using UnityEngine;

public interface IAnnoyableStudent
{
    Transform SeatPoint { get; }
    bool CanBeAnnoyed { get; }
    void BeginBeingAnnoyed(AnnoyingStudent annoyer);
    void StopBeingAnnoyed(AnnoyingStudent annoyer);
}
