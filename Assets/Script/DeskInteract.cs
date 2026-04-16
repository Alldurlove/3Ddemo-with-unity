using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DeskInteract : MonoBehaviour
{
    [Header("Detect")]
    [SerializeField] Camera playerCamera;
    [SerializeField] Transform playerRoot;
    [Tooltip("可选：交互参考点。不填则用高亮 Renderer 包围盒中心（避免默认轴心在桌脚导致屏幕辅助点偏离准星）")]
    [SerializeField] Transform interactionPoint;
    [SerializeField] float rayDistance = 8f;
    [SerializeField] float interactRange = 2.5f;
    [SerializeField] LayerMask rayMask = ~0;
    [Tooltip("交互点在屏幕上离准星(屏幕中心)多近算“瞄到”（Viewport 下与 0.5,0.5 的距离，约 0.08 ≈ 更宽）")]
    [SerializeField] float viewportAimAssistRadius = 0.1f;
    [Tooltip("沿视线的粗检测半径；0 关闭。可略微抵消薄碰撞体难对准的问题")]
    [SerializeField] float sphereCastRadius = 0.08f;

    [Header("Input")]
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [Tooltip("true=必须准星命中才可交互；false=只要在范围内按键即可交互（仍保留高亮判定）")]
    [SerializeField] bool requireFocusForInteract = false;

    [Header("Highlight / Prompt")]
    [SerializeField] Renderer[] highlightRenderers;
    [SerializeField] Color hitEmissionColor = new Color(0.9f, 0.75f, 0.1f, 1f);
    [Tooltip("可选：弱提示（玩家靠近时显示）")]
    [SerializeField] GameObject nearPrompt;
    [Tooltip("可选：强提示（射线命中时显示）")]
    [SerializeField] GameObject focusPrompt;

    [Header("Event")]
    [SerializeField] UnityEvent onInteract;
    [Header("Scene Transition")]
    [Tooltip("按下交互键后直接切换场景（用于进入 2D 解谜）")]
    [SerializeField] bool loadSceneOnInteract;
    [SerializeField] string targetSceneName = "Puzzle2D";
    [Header("Debug")]
    [SerializeField] bool verboseLog;

    struct EmissionCache
    {
        public Material material;
        public bool hasEmission;
        public Color baseEmission;
    }

    readonly List<EmissionCache> _emissionCaches = new List<EmissionCache>();
    bool _isFocused;
    /// <summary>当未指定 interactionPoint 时，在 Awake 用 Renderer 包围盒中心计算，避免 pivot 在角落/桌脚。</summary>
    Vector3 _fallbackInteractWorld;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
        ResolvePlayerRoot();

        if (highlightRenderers == null || highlightRenderers.Length == 0)
            highlightRenderers = GetComponentsInChildren<Renderer>();

        CacheFallbackInteractWorld();
        CacheEmission();
        ApplyFocusVisual(false);
        SetPromptActive(nearPrompt, false);
        SetPromptActive(focusPrompt, false);
    }

    void Update()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;

        bool inRange = IsPlayerInRange();
        bool focusedByRay = IsFocusedByCrosshair();

        SetPromptActive(nearPrompt, inRange && !focusedByRay);
        SetPromptActive(focusPrompt, focusedByRay);
        ApplyFocusVisual(focusedByRay);

        if (Input.GetKeyDown(interactKey))
        {
            bool canInteract = inRange && (!requireFocusForInteract || focusedByRay);
            if (canInteract)
                HandleInteract();
            else if (verboseLog)
                Debug.Log($"[DeskInteract] {name} blocked. focused={focusedByRay}, inRange={inRange}, requireFocus={requireFocusForInteract}, playerRoot={(playerRoot != null ? playerRoot.name : "null")}, cam={(playerCamera != null ? playerCamera.name : "null")}");
        }
    }

    void HandleInteract()
    {
        onInteract?.Invoke();
        if (!loadSceneOnInteract)
            return;
        if (string.IsNullOrWhiteSpace(targetSceneName))
            return;
        SceneManager.LoadScene(targetSceneName);
    }

    public void AddInteractListener(UnityAction callback)
    {
        if (callback != null)
            onInteract.AddListener(callback);
    }

    public void RemoveInteractListener(UnityAction callback)
    {
        if (callback != null)
            onInteract.RemoveListener(callback);
    }

    bool IsPlayerInRange()
    {
        if (playerRoot == null)
            ResolvePlayerRoot();
        if (playerRoot == null)
            return false;
        return Vector3.Distance(playerRoot.position, GetInteractWorldPosition()) <= interactRange;
    }

    void ResolvePlayerRoot()
    {
        if (playerRoot != null)
            return;

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
        {
            playerRoot = go.transform;
            return;
        }

        PlayerMove pm = FindFirstObjectByType<PlayerMove>();
        if (pm != null)
            playerRoot = pm.transform;
    }

    bool IsFocusedByCrosshair()
    {
        if (playerCamera == null)
            return false;

        Ray centerRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (sphereCastRadius > 0.001f &&
            Physics.SphereCast(centerRay.origin, sphereCastRadius, centerRay.direction, out RaycastHit sphereHit, rayDistance, rayMask,
                QueryTriggerInteraction.Collide))
        {
            if (sphereHit.collider != null && sphereHit.collider.GetComponentInParent<DeskInteract>() == this)
                return true;
        }

        if (TryFirstNonPlayerHit(centerRay, out RaycastHit lineHit))
        {
            if (lineHit.collider != null && lineHit.collider.GetComponentInParent<DeskInteract>() == this)
                return true;
        }

        Vector3 worldRef = GetInteractWorldPosition();
        Vector3 vp = playerCamera.WorldToViewportPoint(worldRef);
        if (vp.z <= 0f)
            return false;
        float dx = vp.x - 0.5f;
        float dy = vp.y - 0.5f;
        if (dx * dx + dy * dy > viewportAimAssistRadius * viewportAimAssistRadius)
            return false;

        Vector3 camPos = playerCamera.transform.position;
        Vector3 toPoint = worldRef - camPos;
        float dist = toPoint.magnitude;
        if (dist < 0.01f)
            return true;
        Vector3 dir = toPoint / dist;
        if (Physics.Raycast(camPos, dir, out RaycastHit aimHit, dist + 0.35f, rayMask, QueryTriggerInteraction.Collide))
            return aimHit.collider != null && aimHit.collider.GetComponentInParent<DeskInteract>() == this;

        return false;
    }

    bool TryFirstNonPlayerHit(Ray ray, out RaycastHit bestHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, rayDistance, rayMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            bestHit = default;
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || IsPlayerCollider(h.collider))
                continue;
            bestHit = h;
            return true;
        }

        bestHit = default;
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

    Vector3 GetInteractWorldPosition()
    {
        if (interactionPoint != null)
            return interactionPoint.position;
        return _fallbackInteractWorld;
    }

    void CacheFallbackInteractWorld()
    {
        if (interactionPoint != null)
            return;
        _fallbackInteractWorld = BoundsCenterFromRenderers(highlightRenderers, transform);
    }

    static Vector3 BoundsCenterFromRenderers(Renderer[] rends, Transform rootIfEmpty)
    {
        if (rootIfEmpty == null)
            return Vector3.zero;
        if (rends == null || rends.Length == 0)
            return rootIfEmpty.position;

        bool has = false;
        Bounds merged = default;
        foreach (Renderer r in rends)
        {
            if (r == null)
                continue;
            if (!has)
            {
                merged = r.bounds;
                has = true;
            }
            else
                merged.Encapsulate(r.bounds);
        }

        return has ? merged.center : rootIfEmpty.position;
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
        if (_isFocused == focused)
            return;
        _isFocused = focused;

        foreach (EmissionCache c in _emissionCaches)
        {
            if (c.material == null || !c.hasEmission)
                continue;

            c.material.EnableKeyword("_EMISSION");
            c.material.SetColor("_EmissionColor", focused ? hitEmissionColor : c.baseEmission);
        }
    }

    static void SetPromptActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.3f);
        Vector3 c;
        if (interactionPoint != null)
            c = interactionPoint.position;
        else if (Application.isPlaying)
            c = _fallbackInteractWorld;
        else
        {
            Renderer[] r = highlightRenderers;
            if (r == null || r.Length == 0)
                r = GetComponentsInChildren<Renderer>();
            c = BoundsCenterFromRenderers(r, transform);
        }

        Gizmos.DrawWireSphere(c, interactRange);
    }
}
