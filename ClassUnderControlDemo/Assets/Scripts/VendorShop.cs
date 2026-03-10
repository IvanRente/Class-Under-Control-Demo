using UnityEngine;

public class VendorShop : MonoBehaviour
{
    [SerializeField] string displayName = "Vendor";
    [SerializeField] bool debugInteractions = true;

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return name;

            return displayName;
        }
    }

    public void Interact(PlayerItemSystem itemSystem)
    {
        if (debugInteractions)
        {
            Debug.Log("[VendorShop] Interact called on '" + DisplayName
                + "'. PlayerItemSystem assigned: " + (itemSystem != null)
                + ". GameObject: " + gameObject.name + ".");
        }

        if (itemSystem == null)
            return;

        itemSystem.ToggleShop(this);
    }
}
