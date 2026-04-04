using UnityEngine;

/// <summary>
/// 门交互：在「判定区域」内（Trigger）按交互键开关。
/// 推荐层级：DoorRoot（本脚本 + BoxCollider 勾选 Is Trigger + 可选 Kinematic Rigidbody）→ 子物体 DoorPanel（门板模型，拖到 Door Pivot）。
/// 若 Trigger 做在子物体上，在子物体上加 <see cref="DoorInteractVolume"/>，父级仍挂本脚本。
/// </summary>
public class DoorInteract : MonoBehaviour
{
    [SerializeField] string playerTag = "Player";
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [Tooltip("实际旋转的门板；不指定则旋转本物体（此时判定区会随门一起转）")]
    [SerializeField] Transform doorPivot;
    [Tooltip("相对关闭状态绕门板本地 Y 打开的角度（一般填正数即可）")]
    public float openAngle = 90f;
    [Tooltip("勾选：向外开；取消：向内开（沿本地 Y 反向）")]
    [SerializeField] bool openOutward = true;
    [Tooltip("每秒最大转动角度")]
    public float rotateSpeed = 120f;

    bool isOpen;
    Quaternion closedRot;
    Quaternion openRot;
    int _playersInside;

    Transform Pivot => doorPivot != null ? doorPivot : transform;

    void Awake()
    {
        if (doorPivot == null)
            doorPivot = transform;

        var myTrigger = GetComponent<Collider>();
        if (myTrigger != null && myTrigger.isTrigger)
            EnsureKinematicRigidbody(gameObject);
    }

    void Start()
    {
        closedRot = Pivot.rotation;
        float signedYaw = openOutward ? -openAngle : openAngle;
        openRot = closedRot * Quaternion.Euler(0f, signedYaw, 0f);
    }

    void Update()
    {
        if (_playersInside > 0 && Input.GetKeyDown(interactKey))
            isOpen = !isOpen;

        Quaternion target = isOpen ? openRot : closedRot;
        Pivot.rotation = Quaternion.RotateTowards(
            Pivot.rotation,
            target,
            rotateSpeed * Time.deltaTime
        );
    }

    void OnTriggerEnter(Collider other)
    {
        NotifyPlayerTrigger(1, other);
    }

    void OnTriggerExit(Collider other)
    {
        NotifyPlayerTrigger(-1, other);
    }

    /// <summary>由子物体 <see cref="DoorInteractVolume"/> 调用。</summary>
    public void NotifyPlayerTrigger(int delta, Collider other)
    {
        if (!IsPlayer(other))
            return;
        _playersInside = Mathf.Max(0, _playersInside + delta);
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }

    bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;
        return other.GetComponentInParent<PlayerMove>() != null;
    }

    static void EnsureKinematicRigidbody(GameObject go)
    {
        if (go.GetComponent<Rigidbody>() != null)
            return;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
}

/// <summary>
/// 挂在「单独做判定区」的子物体上（带 Trigger Collider）。父级需有 <see cref="DoorInteract"/>。
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorInteractVolume : MonoBehaviour
{
    DoorInteract _door;

    void Awake()
    {
        _door = GetComponentInParent<DoorInteract>();
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        EnsureKinematicRigidbody(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        _door?.NotifyPlayerTrigger(1, other);
    }

    void OnTriggerExit(Collider other)
    {
        _door?.NotifyPlayerTrigger(-1, other);
    }

    static void EnsureKinematicRigidbody(GameObject go)
    {
        if (go.GetComponent<Rigidbody>() != null)
            return;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
}
