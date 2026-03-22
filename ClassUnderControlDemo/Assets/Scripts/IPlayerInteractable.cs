public interface IPlayerInteractable
{
    bool CanInteract(PlayerController player);
    void Interact(PlayerController player);
}
