using UnityEngine;

public interface IClassStudent
{
    void OnNameCalled();
    void OnClassEnded();
    void LeaveClassroom(Transform exitPoint);
    void PrepareForNewClass();
    void Stun(float duration);
}
