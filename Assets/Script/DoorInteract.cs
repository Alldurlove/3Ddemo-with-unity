using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 新门逻辑：
/// - 未击败 Boss：按 E 仅提示，不能开门
/// - 已击败 Boss：按 E 进入 2D 谜题场景
/// 使用触发区判定玩家是否在门前。
/// </summary>
public class DoorInteract : MonoBehaviour
{
    struct EmissionCache
    {
        public Material material;
        public bool hasEmission;
        public Color baseEmission;
    }

    [Header("Input")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] Transform playerRoot;
    [Tooltip("触发区失败时的距离兜底：在门附近也可按 E 交互")]
    [SerializeField] bool useDistanceFallback = true;
    [SerializeField] float interactDistance = 2.8f;

    [Header("Scene")]
    [SerializeField] string bossSceneName = "BossScene";
    [SerializeField] string puzzleSceneName = "Puzzle2D";

    [Header("Messages")]
    [SerializeField] string firstEnterBossMessage = "异样的门扉被触发，进入Boss空间。";
    [SerializeField] string lockedMessage = "门被封锁了，先击败Boss。";
    [SerializeField] string openMessage = "门已开启，进入谜题空间。";

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

    int _playersInside;
    readonly List<EmissionCache> _emissionCaches = new List<EmissionCache>();
    bool _isFocusedByRay;

    void Awake()
    {
        var myTrigger = GetComponent<Collider>();
        if (myTrigger != null && myTrigger.isTrigger)
            EnsureKinematicRigidbody(gameObject);

        ResolvePlayerRoot();

        if (highlightCamera == null)
            highlightCamera = Camera.main;
        if (highlightRenderers == null || highlightRenderers.Length == 0)
            highlightRenderers = GetComponentsInChildren<Renderer>();
        CacheEmission();
        ApplyFocusVisual(false);
        SetPromptActive(false);
    }

    void Update()
    {
        if (enableRayHighlight)
            UpdateRayHighlight();

        if (!Input.GetKeyDown(interactKey))
            return;

        bool inTrigger = _playersInside > 0;
        bool inDistance = useDistanceFallback && IsPlayerInDistance();
        bool canInteract = inTrigger || inDistance;
        if (!canInteract)
        {
            if (verboseLog)
                Debug.Log($"[DoorInteract] blocked. inTrigger={inTrigger}, inDistance={inDistance}, playersInside={_playersInside}");
            return;
        }

        // 第一次交互：优先进入 Boss 场景
        if (!GameState.bossSceneEntered)
        {
            GameState.bossSceneEntered = true;
            if (verboseLog)
                Debug.Log(firstEnterBossMessage);
            if (!string.IsNullOrWhiteSpace(bossSceneName))
                SceneManager.LoadScene(bossSceneName);
            return;
        }

        // 第二次及以后：保持原有逻辑不变（未击败Boss不让进谜题）
        if (!GameState.bossDefeated)
        {
            if (verboseLog)
                Debug.Log(lockedMessage);
            return;
        }

        if (verboseLog)
            Debug.Log(openMessage);
        if (!string.IsNullOrWhiteSpace(puzzleSceneName))
            SceneManager.LoadScene(puzzleSceneName);
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

    bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;
        return other.GetComponentInParent<PlayerMove>() != null;
    }

    bool IsPlayerInDistance()
    {
        if (playerRoot == null)
            ResolvePlayerRoot();
        if (playerRoot == null)
            return false;
        return Vector3.Distance(playerRoot.position, transform.position) <= interactDistance;
    }

    void ResolvePlayerRoot()
    {
        if (playerRoot != null)
            return;
        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null)
        {
            playerRoot = go.transform;
            return;
        }

        PlayerMove pm = FindFirstObjectByType<PlayerMove>();
        if (pm != null)
            playerRoot = pm.transform;
    }

    void UpdateRayHighlight()
    {
        if (highlightCamera == null && Camera.main != null)
            highlightCamera = Camera.main;
        if (highlightCamera == null)
            return;

        bool focused = IsFocusedByCrosshair();
        ApplyFocusVisual(focused);
        SetPromptActive(focused);
    }

    bool IsFocusedByCrosshair()
    {
        Ray ray = highlightCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, highlightRayDistance, highlightRayMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return false;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || IsPlayer(h.collider))
                continue;
            DoorInteract door = h.collider.GetComponentInParent<DoorInteract>();
            return door == this;
        }
        return false;
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
