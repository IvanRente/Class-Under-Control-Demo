using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float lookSpeed = 2f;
    public Transform cam;

    public float interactDistance = 5f;

    [Header("Debug")]
    public bool debugVendorInteraction = true;

    CharacterController cc;
    PlayerItemSystem itemSystem;
    float pitch;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        itemSystem = GetComponent<PlayerItemSystem>();
        LockCursor(true);
    }

    void Update()
    {
        if (itemSystem != null && itemSystem.IsAnyMenuOpen)
        {
            if (debugVendorInteraction && Input.GetKeyDown(itemSystem.interactKey))
                Debug.Log("[PlayerController] Interact ignored because a menu is already open.");

            return;
        }

        HandleLook();
        HandleMove();
        HandleInteract();
    }

    void HandleLook()
    {
        float mx = Input.GetAxis("Mouse X") * lookSpeed;
        float my = Input.GetAxis("Mouse Y") * lookSpeed;

        transform.Rotate(Vector3.up * mx);
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        cam.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    void HandleMove()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 dir = transform.right * h + transform.forward * v;
        cc.SimpleMove(dir * moveSpeed);
    }

    void HandleInteract()
    {
        KeyCode interactKey = itemSystem != null ? itemSystem.interactKey : KeyCode.E;
        bool pressedInteract = Input.GetKeyDown(interactKey);
        Ray ray = new Ray(cam.position, cam.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            string vendorResolution;
            VendorShop vendor = FindVendor(hit.collider, out vendorResolution);

            if (pressedInteract && debugVendorInteraction)
            {
                Debug.Log("[PlayerController] Interact ray hit '" + hit.collider.name
                    + "' at distance " + hit.distance.ToString("0.00")
                    + ". Vendor resolution: " + vendorResolution
                    + ". ItemSystem assigned: " + (itemSystem != null) + ".");
            }

            if (vendor != null && itemSystem != null && pressedInteract)
            {
                if (debugVendorInteraction)
                    Debug.Log("[PlayerController] Opening vendor '" + vendor.DisplayName + "' via " + vendorResolution + ".");

                vendor.Interact(itemSystem);
                return;
            }

            AnswerHitZone zone = hit.collider.GetComponent<AnswerHitZone>();
            if (zone != null)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Debug.Log("Right click on answer index " + zone.answerIndex);
                    zone.quizBoard.AnswerButton(zone.answerIndex);
                }
            }
        }
        else if (pressedInteract && debugVendorInteraction)
        {
            Debug.Log("[PlayerController] Interact ray did not hit anything within " + interactDistance.ToString("0.00") + " units.");
        }

        if (pressedInteract && debugVendorInteraction && itemSystem != null)
            Debug.Log("[PlayerController] No vendor was opened from the current raycast hit.");
    }

    VendorShop FindVendor(Collider hitCollider, out string resolution)
    {
        resolution = "none";

        if (hitCollider == null)
            return null;

        VendorShop vendor = hitCollider.GetComponent<VendorShop>();
        if (vendor != null)
        {
            resolution = "collider";
            return vendor;
        }

        vendor = hitCollider.GetComponentInParent<VendorShop>();
        if (vendor != null)
        {
            resolution = "parent";
            return vendor;
        }

        vendor = hitCollider.transform.root.GetComponentInChildren<VendorShop>(true);
        if (vendor != null)
            resolution = "root";

        return vendor;
    }
    void LockCursor(bool doLock)
    {
        if (doLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
