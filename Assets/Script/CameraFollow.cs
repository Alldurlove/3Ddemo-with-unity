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
    [Header("Obstacle Avoidance")]
    [SerializeField] LayerMask obstacleMask = ~0;
    [SerializeField] float cameraRadius = 0.2f;
    [SerializeField] float wallPadding = 0.15f;
    [SerializeField] float minDistanceFromTarget = 0.6f;

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
        desiredPosition = ResolveObstacles(lookAtPoint, desiredPosition);

        if (followSmoothTime > 0.0001f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime);
        else
            transform.position = desiredPosition;

        transform.rotation = viewRotation;
    }

    Vector3 ResolveObstacles(Vector3 lookAtPoint, Vector3 desiredPosition)
    {
        Vector3 dir = desiredPosition - lookAtPoint;
        float targetDistance = dir.magnitude;
        if (targetDistance < 0.0001f)
            return desiredPosition;

        dir /= targetDistance;
        RaycastHit[] hits = Physics.SphereCastAll(
            lookAtPoint,
            cameraRadius,
            dir,
            targetDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        if (hits == null || hits.Length == 0)
            return desiredPosition;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;
            if (hit.collider.transform == target || hit.collider.transform.IsChildOf(target))
                continue;

            float clippedDistance = Mathf.Max(minDistanceFromTarget, hit.distance - wallPadding);
            return lookAtPoint + dir * clippedDistance;
        }

        return desiredPosition;
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