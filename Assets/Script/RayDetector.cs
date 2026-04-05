using UnityEngine;

public class RayDetector : MonoBehaviour
{
    [SerializeField] Camera detectCamera;
    [SerializeField] float detectDistance = 8f;
    [SerializeField] LayerMask detectLayers = ~0;
    [SerializeField] KeyCode detectKey = KeyCode.E;
    [Tooltip("true=从屏幕中心发射；false=从鼠标位置发射")]
    [SerializeField] bool fromScreenCenter = true;

    void Awake()
    {
        if (detectCamera == null)
            detectCamera = Camera.main;
    }

    void Update()
    {
        if (!Input.GetKeyDown(detectKey))
            return;

        if (detectCamera == null)
            return;

        Ray ray = fromScreenCenter
            ? detectCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : detectCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, detectDistance, detectLayers, QueryTriggerInteraction.Collide))
            Debug.Log("Ray Hit: " + hit.collider.name, hit.collider.gameObject);
        else
            Debug.Log("Ray Hit: None");
    }
}
