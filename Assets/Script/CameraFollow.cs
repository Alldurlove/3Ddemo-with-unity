using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public enum ShoulderSide
    {
        Right,
        Left
    }

    [SerializeField] Transform target;
    [Tooltip("相机在左肩还是右肩（沿角色本地 X 偏移）")]
    [SerializeField] ShoulderSide shoulderSide = ShoulderSide.Right;
    [Tooltip("相对角色本地空间：X 填「与肩的水平距离」（恒为正，左右由肩侧决定）；上为正 Y，后为负 Z（拉近则减小 |Z|）")]
    [SerializeField] Vector3 offset = new Vector3(0.52f, 2.1f, -2.25f);
    [SerializeField] bool offsetInLocalSpace = true;
    [Tooltip("越小越跟得紧；0 表示不插值")]
    [SerializeField] float smoothTime = 0.12f;
    [Tooltip("看向目标时的高度偏移（相对脚底）")]
    [SerializeField] float lookAtHeight = 1.15f;
    [Tooltip("看向目标时左右偏移幅度（米，恒>=0，符号与肩侧一致，便于准星对准前方）")]
    [SerializeField] float lookAtLateralShift = 0.12f;

    [Tooltip("鼠标上下转动视角的速度")]
    [SerializeField] float mouseSensitivityY = 2f;
    [SerializeField] float pitchMin = -40f;
    [SerializeField] float pitchMax = 55f;

    Vector3 _velocity;
    float _pitch;

    void LateUpdate()
    {
        if (target == null) return;

        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY;
        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        float sideSign = shoulderSide == ShoulderSide.Right ? 1f : -1f;
        Vector3 o = new Vector3(Mathf.Abs(offset.x) * sideSign, offset.y, offset.z);
        Vector3 worldOffset = offsetInLocalSpace ? target.TransformDirection(o) : o;
        Vector3 desiredPosition = target.position + worldOffset;

        if (smoothTime > 0.0001f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, smoothTime);
        else
            transform.position = desiredPosition;

        float lateralLook = Mathf.Abs(lookAtLateralShift) * sideSign;
        Vector3 lookAt = target.position + target.right * lateralLook + Vector3.up * lookAtHeight;
        transform.LookAt(lookAt);
        transform.rotation = Quaternion.AngleAxis(_pitch, transform.right) * transform.rotation;
    }
}
