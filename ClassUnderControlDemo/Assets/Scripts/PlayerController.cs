using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float lookSpeed = 2f;
    public Transform cam;

    public float interactDistance = 5f;

    public bool debugVendorInteraction = true;

    CharacterController cc;
    PlayerItemSystem itemSystem;
    Camera playerCamera;
    float pitch;
    FireHazard heldFire;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        itemSystem = GetComponent<PlayerItemSystem>();
        if (cam != null)
        {
            playerCamera = cam.GetComponent<Camera>();
            if (playerCamera == null)
                playerCamera = cam.GetComponentInChildren<Camera>();
        }

        LockCursor(true);
    }

    void Update()
    {
        if (itemSystem != null && itemSystem.IsAnyMenuOpen)
        {
            ClearHeldFire();

            if (debugVendorInteraction && Input.GetKeyDown(itemSystem.interactKey))
                Debug.Log("[PlayerController] Interact ignored because a menu is already open.");

            return;
        }

        HandleLook();
        HandleMove();
        bool consumedPrimaryAction = HandlePrimaryItemAction();
        HandleInteract(consumedPrimaryAction);
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

    bool HandlePrimaryItemAction()
    {
        if (itemSystem == null)
            return false;

        return itemSystem.TryHandlePrimaryAction(
            playerCamera,
            Input.GetMouseButtonDown(1),
            Input.GetMouseButton(1),
            Input.GetMouseButtonUp(1));
    }

    void HandleInteract(bool suppressPrimaryAction)
    {
        KeyCode interactKey = itemSystem != null ? itemSystem.interactKey : KeyCode.E;
        bool pressedInteract = Input.GetKeyDown(interactKey);
        bool pressedPrimary = !suppressPrimaryAction && Input.GetMouseButtonDown(0);
        bool holdingPrimary = !suppressPrimaryAction && Input.GetMouseButton(0);
        Ray ray = new Ray(cam.position, cam.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            FireHazard fire = FindFireHazard(hit.collider);
            if (fire != null && fire.IsLit)
                HandleFireInteraction(fire, holdingPrimary);
            else
                ClearHeldFire();

            string vendorResolution;
            VendorShop vendor = FindVendor(hit.collider, out vendorResolution);
            IPlayerInteractable interactable = FindInteractable(hit.collider);

            if (pressedInteract && debugVendorInteraction)
            {
                Debug.Log("[PlayerController] Interact ray hit '" + hit.collider.name
                    + "' at distance " + hit.distance.ToString("0.00")
                    + ". Vendor resolution: " + vendorResolution
                    + ". ItemSystem assigned: " + (itemSystem != null) + ".");
            }

            if (interactable != null && pressedInteract && interactable.CanInteract(this))
            {
                interactable.Interact(this);
                return;
            }

            if (vendor != null && itemSystem != null && pressedInteract)
            {
                if (debugVendorInteraction)
                    Debug.Log("[PlayerController] Opening vendor '" + vendor.DisplayName + "' via " + vendorResolution + ".");

                vendor.Interact(itemSystem);
                return;
            }

            IPrimaryClickReceiver primaryClickReceiver = FindPrimaryClickReceiver(hit.collider);
            if (primaryClickReceiver != null && pressedPrimary)
            {
                primaryClickReceiver.OnPrimaryClick(this);
                return;
            }
        }
        else if (pressedInteract && debugVendorInteraction)
        {
            ClearHeldFire();
            Debug.Log("[PlayerController] Interact ray did not hit anything within " + interactDistance.ToString("0.00") + " units.");
        }
        else
        {
            ClearHeldFire();
        }

        if (pressedInteract && debugVendorInteraction && itemSystem != null)
            Debug.Log("[PlayerController] No vendor was opened from the current raycast hit.");
    }

    void HandleFireInteraction(FireHazard fire, bool holdingPrimary)
    {
        if (fire == null)
        {
            ClearHeldFire();
            return;
        }

        if (heldFire != null && heldFire != fire)
            heldFire.CancelExtinguishHold();

        heldFire = fire;

        if (holdingPrimary)
            heldFire.ProgressExtinguishHold(Time.deltaTime);
        else
            heldFire.CancelExtinguishHold();
    }

    void ClearHeldFire()
    {
        if (heldFire == null) return;

        heldFire.CancelExtinguishHold();
        heldFire = null;
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

    IPrimaryClickReceiver FindPrimaryClickReceiver(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        IPrimaryClickReceiver receiver = hitCollider.GetComponent<IPrimaryClickReceiver>();
        if (receiver != null)
            return receiver;

        return hitCollider.GetComponentInParent<IPrimaryClickReceiver>();
    }

    IPlayerInteractable FindInteractable(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        IPlayerInteractable interactable = hitCollider.GetComponent<IPlayerInteractable>();
        if (interactable != null)
            return interactable;

        return hitCollider.GetComponentInParent<IPlayerInteractable>();
    }

    FireHazard FindFireHazard(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        FireHazard fire = hitCollider.GetComponent<FireHazard>();
        if (fire != null)
            return fire;

        fire = hitCollider.GetComponentInParent<FireHazard>();
        if (fire != null)
            return fire;

        return hitCollider.transform.root.GetComponentInChildren<FireHazard>(true);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null) return;

        AnnoyingStudent annoyingStudent = hit.collider.GetComponentInParent<AnnoyingStudent>();
        if (annoyingStudent != null && annoyingStudent.isActiveAndEnabled)
            annoyingStudent.NotifyPlayerCollision();

        PyromaniacStudent pyromaniacStudent = hit.collider.GetComponentInParent<PyromaniacStudent>();
        if (pyromaniacStudent != null && pyromaniacStudent.isActiveAndEnabled)
            pyromaniacStudent.NotifyPlayerCollision();
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

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (cc == null)
            cc = GetComponent<CharacterController>();

        bool hadController = cc != null;
        bool wasEnabled = hadController && cc.enabled;

        if (wasEnabled)
            cc.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (cam != null)
        {
            cam.localRotation = Quaternion.identity;
            pitch = 0f;
        }

        if (wasEnabled)
            cc.enabled = true;
    }
}
