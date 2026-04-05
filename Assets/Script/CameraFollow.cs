using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float distance = 6f;
    [SerializeField] float lookAtHeight = 1.6f;
    [SerializeField] float mouseSensitivityX = 2f;
    [SerializeField] float mouseSensitivityY = 2f;
    [SerializeField] float pitchMin = -35f;
    [SerializeField] float pitchMax = 65f;
    [SerializeField] float followSmoothTime = 0.06f;

    float yaw;
    float pitch;
    Vector3 followVelocity;

    public Transform Target => target;

    void LateUpdate()
    {
        if (target == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeight;
        Quaternion viewRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = lookAtPoint - viewRotation * Vector3.forward * distance;

        if (followSmoothTime > 0.0001f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime);
        else
            transform.position = desiredPosition;

        transform.rotation = viewRotation;
    }

    void OnEnable()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = NormalizePitch(euler.x);
    }

    static float NormalizePitch(float angle)
    {
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}