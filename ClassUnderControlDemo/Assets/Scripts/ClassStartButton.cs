using UnityEngine;

public class ClassStartButton : MonoBehaviour, IPlayerInteractable
{
    public ClassTransitionFlowController flowController;

    public bool CanInteract(PlayerController player)
    {
        return flowController != null && flowController.CanStartNextClassFromButton;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        flowController.RequestStartNextClass();
    }

    public void SetFlowController(ClassTransitionFlowController controller)
    {
        flowController = controller;
    }

}
