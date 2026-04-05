using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DeskInteract : MonoBehaviour
{
    [Header("Detect")]
    [SerializeField] Camera playerCamera;
    [SerializeField] Transform playerRoot;
    [SerializeField] Transform interactionPoint;
    [SerializeField] float rayDistance = 8f;
    [SerializeField] float interactRange = 2.5f;
    [SerializeField] LayerMask rayMask = ~0;

    [Header("Input")]
    [SerializeField] KeyCode interactKey = KeyCode.E;

    [Header("Highlight / Prompt")]
    [SerializeField] Renderer[] highlightRenderers;
    [SerializeField] Color hitEmissionColor = new Color(0.9f, 0.75f, 0.1f, 1f);
    [Tooltip("可选：弱提示（玩家靠近时显示）")]
    [SerializeField] GameObject nearPrompt;
    [Tooltip("可选：强提示（射线命中时显示）")]
    [SerializeField] GameObject focusPrompt;

    [Header("Event")]
    [SerializeField] UnityEvent onInteract;

    struct EmissionCache
    {
        public Material material;
        public bool hasEmission;
        public Color baseEmission;
    }

    readonly List<EmissionCache> _emissionCaches = new List<EmissionCache>();
    bool _isFocused;

    Transform Point => interactionPoint != null ? interactionPoint : transform;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
        if (playerRoot == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                playerRoot = go.transform;
        }

        if (highlightRenderers == null || highlightRenderers.Length == 0)
            highlightRenderers = GetComponentsInChildren<Renderer>();

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
        bool focusedByRay = IsHitByRay();

        SetPromptActive(nearPrompt, inRange && !focusedByRay);
        SetPromptActive(focusPrompt, focusedByRay);
        ApplyFocusVisual(focusedByRay);

        if (focusedByRay && inRange && Input.GetKeyDown(interactKey))
            onInteract?.Invoke();
    }

    bool IsPlayerInRange()
    {
        if (playerRoot == null)
            return false;
        return Vector3.Distance(playerRoot.position, Point.position) <= interactRange;
    }

    bool IsHitByRay()
    {
        if (playerCamera == null)
            return false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, rayMask, QueryTriggerInteraction.Collide))
            return false;

        DeskInteract desk = hit.collider.GetComponentInParent<DeskInteract>();
        return desk == this;
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
        Gizmos.DrawWireSphere(Point.position, interactRange);
    }
}
