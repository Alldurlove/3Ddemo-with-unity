using UnityEngine;

public class GunShoot : MonoBehaviour
{
    public Camera cam;
    public float range = 100f;
    [Tooltip("单次命中对敌人造成的伤害")]
    [SerializeField] int damage = 1;
    [Header("Raycast")]
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField] bool hitTriggers = true;
    [Header("Shoot Lock")]
    [Tooltip("开枪后短暂禁止移动输入，避免滑步")]
    [SerializeField] float lockControlDuration = 0.25f;
    [Header("Aim Rotation")]
    [Tooltip("开火前将角色朝向准星方向（仅水平旋转）")]
    [SerializeField] bool faceCrosshairOnShoot = true;
    [SerializeField] Transform characterRoot;
    Animator animator;
    PlayerMove playerMove;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
        if (animator == null)
            animator = GetComponentInParent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        playerMove = GetComponentInParent<PlayerMove>();
        if (characterRoot == null && playerMove != null)
            characterRoot = playerMove.transform;
        if (characterRoot == null)
            characterRoot = transform.root;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            Shoot();
    }

    void Shoot()
    {
        if (cam == null)
            return;

        // 与交互共用屏幕中心准星
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hasHit = TryGetFirstNonPlayerHit(ray, range, hitMask, hitTriggers, out RaycastHit hit);
        Vector3 aimPoint = hasHit ? hit.point : (ray.origin + ray.direction * range);

        if (faceCrosshairOnShoot)
            FaceAimPoint(aimPoint);

        if (animator != null)
            animator.SetTrigger("Shoot");
        if (playerMove != null)
            playerMove.LockControls(lockControlDuration);

        if (!hasHit)
        {
            Debug.Log("[GunShoot] No valid hit.");
            return;
        }

        BossHealth boss = hit.collider.GetComponentInParent<BossHealth>();
        if (boss != null)
        {
            boss.TakeDamage(damage);
            Debug.Log("Hit: " + hit.collider.name + $" (Boss HP: {boss.CurrentHealth})");
            return;
        }

        EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
            enemy.TakeDamage(damage);
        else
            hit.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        Debug.Log("Hit: " + hit.collider.name + (enemy != null ? $" (Enemy HP: {enemy.CurrentHealth})" : ""));
    }

    /// <summary>越肩视角射线会先打到自己，按距离取第一个不是玩家的碰撞体。</summary>
    static bool TryGetFirstNonPlayerHit(Ray ray, float maxDistance, LayerMask mask, bool includeTriggers, out RaycastHit hit)
    {
        QueryTriggerInteraction triggerMode = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, maxDistance, mask, triggerMode);
        if (hits == null || hits.Length == 0)
        {
            hit = default;
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (IsPlayerCollider(h.collider))
                continue;
            hit = h;
            return true;
        }

        hit = default;
        return false;
    }

    static bool IsPlayerCollider(Collider col)
    {
        if (col == null)
            return false;
        if (col.CompareTag("Player"))
            return true;
        return col.GetComponentInParent<PlayerMove>() != null;
    }

    void FaceAimPoint(Vector3 aimPoint)
    {
        if (characterRoot == null)
            return;

        Vector3 toAim = aimPoint - characterRoot.position;
        toAim.y = 0f;
        if (toAim.sqrMagnitude < 0.0001f)
            return;

        characterRoot.rotation = Quaternion.LookRotation(toAim.normalized, Vector3.up);
    }
}
