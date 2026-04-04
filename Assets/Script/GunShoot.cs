using UnityEngine;

public class GunShoot : MonoBehaviour
{
    public Camera cam;
    public float range = 100f;
    [Tooltip("单次命中对敌人造成的伤害")]
    [SerializeField] int damage = 1;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Start()
    {
        ApplyLockedCursor();
    }

    /// <summary>从其它窗口点回游戏时，继续保持锁定（可按需删掉）。</summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ApplyLockedCursor();
    }

    static void ApplyLockedCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!TryGetFirstNonPlayerHit(ray, range, out RaycastHit hit))
            return;

        EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
            enemy.TakeDamage(damage);

        Debug.Log("Hit: " + hit.collider.name + (enemy != null ? $" (Enemy HP: {enemy.CurrentHealth})" : ""));
    }

    /// <summary>越肩视角射线会先打到自己，按距离取第一个不是玩家的碰撞体。</summary>
    static bool TryGetFirstNonPlayerHit(Ray ray, float maxDistance, out RaycastHit hit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance);
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
}
