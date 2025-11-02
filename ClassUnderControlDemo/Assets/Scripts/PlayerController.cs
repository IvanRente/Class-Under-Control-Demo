using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float lookSpeed = 2f;
    public Transform cam;

    CharacterController cc;
    float pitch;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        float mx = Input.GetAxis("Mouse X") * lookSpeed;
        float my = Input.GetAxis("Mouse Y") * lookSpeed;

        transform.Rotate(Vector3.up * mx);
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        cam.localRotation = Quaternion.Euler(pitch, 0, 0);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 dir = transform.right * h + transform.forward * v;
        cc.SimpleMove(dir * moveSpeed);
    }
}
