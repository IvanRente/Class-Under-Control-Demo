using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float lookSpeed = 2f;
    public Transform cam;

    public float interactDistance = 5f;
    CharacterController cc;
    float pitch;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        LockCursor(true);
    }

    void Update()
    {
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
        Ray ray = new Ray(cam.position, cam.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            Debug.Log("Ray hit: " + hit.collider.name);
            AnswerHitZone zone = hit.collider.GetComponent<AnswerHitZone>();
            if (zone != null)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    Debug.Log("Right click on answer index " + zone.answerIndex);
                    zone.quizBoard.AnswerButton(zone.answerIndex);
                }
            }
        }
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
