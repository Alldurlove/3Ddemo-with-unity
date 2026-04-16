using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteract : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("默认关闭。当前项目建议改用 DeskInteract 的 onInteract 事件触发 CameraTrap。")]
    [SerializeField] bool enableRayInteract = false;
    [SerializeField] bool verboseLog = true;

    [Header("Input")]
    [SerializeField] KeyCode interactKey = KeyCode.E;

    [Header("Raycast")]
    [SerializeField] Camera viewCamera;
    [SerializeField] float interactDistance = 8f;
    [SerializeField] LayerMask interactMask = ~0;
    [SerializeField] float sphereCastRadius = 0.08f;

    void Awake()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;
    }

    void Update()
    {
        if (!enableRayInteract)
            return;
        if (!Input.GetKeyDown(interactKey))
            return;
        if (viewCamera == null && Camera.main != null)
            viewCamera = Camera.main;
        if (viewCamera == null)
        {
            if (verboseLog)
                Debug.LogWarning("[PlayerInteract] No camera assigned.");
            return;
        }

        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!TryGetFirstNonPlayerHit(ray, interactDistance, interactMask, sphereCastRadius, out RaycastHit hit))
        {
            if (verboseLog)
                Debug.Log("[PlayerInteract] No hit under crosshair.");
            return;
        }

        MultiStepInteract multi = hit.collider.GetComponentInParent<MultiStepInteract>();
        if (multi != null)
        {
            if (verboseLog)
                Debug.Log($"[PlayerInteract] MultiStepInteract hit: {multi.name}");
            multi.Interact();
            return;
        }

        CameraTrap trap = hit.collider.GetComponentInParent<CameraTrap>();
        if (trap != null)
        {
            if (verboseLog)
                Debug.Log($"[PlayerInteract] CameraTrap hit: {trap.name}");
            trap.ActivateCamera();
            return;
        }

        if (verboseLog)
            Debug.Log($"[PlayerInteract] Hit {hit.collider.name}, but no interactable component found.");
    }

    static bool TryGetFirstNonPlayerHit(Ray ray, float maxDistance, LayerMask mask, float sphereRadius, out RaycastHit hit)
    {
        if (sphereRadius > 0.001f && Physics.SphereCast(ray.origin, sphereRadius, ray.direction, out RaycastHit sphereHit, maxDistance, mask, QueryTriggerInteraction.Collide))
        {
            if (!IsPlayerCollider(sphereHit.collider))
            {
                hit = sphereHit;
                return true;
            }
        }

        RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, maxDistance, mask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            hit = default;
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || IsPlayerCollider(h.collider))
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
