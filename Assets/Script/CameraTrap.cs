using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class CameraTrap : MonoBehaviour
{
    struct EmissionCache
    {
        public Material material;
        public bool hasEmission;
        public Color baseEmission;
    }

    [Header("References")]
    [Tooltip("可选：用于检测方向的相机 Transform；不填则使用本物体")]
    [SerializeField] Transform cameraPoint;
    [SerializeField] BossController targetBoss;
    [SerializeField] DeskInteract deskInteract;
    [Tooltip("自动将 ActivateCamera 绑定到 DeskInteract.onInteract")]
    [SerializeField] bool autoBindDeskInteract = true;
    [Header("Debug")]
    [SerializeField] bool verboseLog = true;
    [Header("Ray Highlight")]
    [SerializeField] bool enableRayHighlight = true;
    [SerializeField] Camera highlightCamera;
    [SerializeField] float highlightRayDistance = 8f;
    [SerializeField] LayerMask highlightRayMask = ~0;
    [SerializeField] Renderer[] highlightRenderers;
    [SerializeField] Color highlightEmissionColor = new Color(0.9f, 0.75f, 0.1f, 1f);
    [SerializeField] GameObject focusPrompt;

    [Header("Detection")]
    [SerializeField] float detectionDistance = 15f;
    [SerializeField] float viewAngle = 45f;
    [Tooltip("阻挡层（墙体等）。建议排除 Boss 与 Player 层")]
    [SerializeField] LayerMask obstacleMask = ~0;

    [Header("Stun")]
    [SerializeField] float stunDuration = 3f;

    Transform CameraPoint => cameraPoint != null ? cameraPoint : transform;
    readonly List<EmissionCache> _emissionCaches = new List<EmissionCache>();
    bool _isFocusedByRay;

    void Awake()
    {
        ResolveDeskInteract();
        if (highlightCamera == null)
            highlightCamera = Camera.main;
        if (highlightRenderers == null || highlightRenderers.Length == 0)
            highlightRenderers = GetComponentsInChildren<Renderer>();
        CacheEmission();
        ApplyFocusVisual(false);
        SetPromptActive(false);
    }

    void OnEnable()
    {
        TryBindDeskInteract();
    }

    void Start()
    {
        // 再次尝试一次，避免执行顺序导致的早期绑定失败
        TryBindDeskInteract();
    }

    void Update()
    {
        if (!enableRayHighlight)
            return;
        if (highlightCamera == null && Camera.main != null)
            highlightCamera = Camera.main;

        bool focused = IsFocusedByCrosshair();
        ApplyFocusVisual(focused);
        SetPromptActive(focused);
    }

    void ResolveDeskInteract()
    {
        if (deskInteract != null)
            return;
        deskInteract = GetComponent<DeskInteract>();
        if (deskInteract == null)
            deskInteract = GetComponentInParent<DeskInteract>();
    }

    void TryBindDeskInteract()
    {
        if (!autoBindDeskInteract)
            return;

        ResolveDeskInteract();
        if (deskInteract == null)
        {
            if (verboseLog)
                Debug.LogWarning($"[CameraTrap] {name} auto-bind failed: no DeskInteract found on self/parent.");
            return;
        }

        deskInteract.RemoveInteractListener(ActivateCamera);
        deskInteract.AddInteractListener(ActivateCamera);
        if (verboseLog)
            Debug.Log($"[CameraTrap] {name} bound to DeskInteract on {deskInteract.name}.");
    }

    public void ActivateCamera()
    {
        if (verboseLog)
            Debug.Log($"[CameraTrap] Activated by interaction: {name}");

        if (!GameState.hasBattery)
        {
            Debug.Log("没电了");
            return;
        }

        if (targetBoss == null)
        {
            Debug.LogWarning($"[CameraTrap] targetBoss is null on {name}");
            return;
        }

        if (CanSeeBoss(targetBoss))
        {
            Debug.Log($"[CameraTrap] Boss detected by {name}, applying stun.");
            targetBoss.RevealAndStun(stunDuration);
        }
        else
        {
            Debug.Log($"[CameraTrap] Boss not detected by {name}.");
        }
    }

    bool CanSeeBoss(BossController boss)
    {
        Transform from = CameraPoint;
        Transform to = boss.Center;
        Vector3 toBoss = to.position - from.position;
        float dist = toBoss.magnitude;

        if (dist > detectionDistance)
            return false;

        float angle = Vector3.Angle(from.forward, toBoss);
        if (angle > viewAngle * 0.5f)
            return false;

        if (dist < 0.001f)
            return true;

        Vector3 dir = toBoss / dist;
        // 只检测障碍层；如果途中有障碍即判定不可见
        if (Physics.Raycast(from.position, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // 命中的不是 Boss 子层级，说明被挡住
            if (hit.transform != to && hit.transform.GetComponentInParent<BossController>() != boss)
                return false;
        }

        return true;
    }

    bool IsFocusedByCrosshair()
    {
        if (highlightCamera == null)
            return false;

        Ray ray = highlightCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, highlightRayDistance, highlightRayMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return false;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || IsPlayerCollider(h.collider))
                continue;
            CameraTrap trap = h.collider.GetComponentInParent<CameraTrap>();
            return trap == this;
        }
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

    void CacheEmission()
    {
        _emissionCaches.Clear();
        if (highlightRenderers == null)
            return;
        foreach (Renderer r in highlightRenderers)
        {
            if (r == null)
                continue;
            Material m = r.material;
            bool hasEmission = m.HasProperty("_EmissionColor");
            Color baseEmission = hasEmission ? m.GetColor("_EmissionColor") : Color.black;
            _emissionCaches.Add(new EmissionCache
            {
                material = m,
                hasEmission = hasEmission,
                baseEmission = baseEmission
            });
        }
    }

    void ApplyFocusVisual(bool focused)
    {
        if (_isFocusedByRay == focused)
            return;
        _isFocusedByRay = focused;

        foreach (EmissionCache c in _emissionCaches)
        {
            if (c.material == null || !c.hasEmission)
                continue;
            c.material.EnableKeyword("_EMISSION");
            c.material.SetColor("_EmissionColor", focused ? highlightEmissionColor : c.baseEmission);
        }
    }

    void SetPromptActive(bool active)
    {
        if (focusPrompt != null && focusPrompt.activeSelf != active)
            focusPrompt.SetActive(active);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Transform cp = CameraPoint;
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(cp.position, detectionDistance);

        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * cp.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * cp.forward;
        Gizmos.DrawLine(cp.position, cp.position + left * detectionDistance);
        Gizmos.DrawLine(cp.position, cp.position + right * detectionDistance);
    }
#endif
}
