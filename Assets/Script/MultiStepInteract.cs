using UnityEngine;

[DisallowMultipleComponent]
public class MultiStepInteract : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] DeskInteract deskInteract;
    [SerializeField] bool autoBindDeskInteract = true;

    [Header("State")]
    [SerializeField] int stage = 0;
    [SerializeField] bool interactionEnabled = true;

    [Header("Text")]
    [SerializeField] string stage0Text = "眼熟的相机 似乎在哪里见过";
    [SerializeField] string stage1Text = "可惜已经损毁了，不过电池倒是还有能量，不愧是混沌之力";
    [SerializeField] string stage2Text = "获得混沌能源块";

    [Header("Reward")]
    [SerializeField] string rewardItemID = "Battery";

    public int Stage => stage;
    public bool InteractionEnabled => interactionEnabled;

    void Awake()
    {
        if (!autoBindDeskInteract)
            return;
        if (deskInteract == null)
            deskInteract = GetComponent<DeskInteract>();
        if (deskInteract == null)
            deskInteract = GetComponentInParent<DeskInteract>();
        if (deskInteract != null)
        {
            deskInteract.RemoveInteractListener(Interact);
            deskInteract.AddInteractListener(Interact);
        }
    }

    /// <summary>
    /// 供 PlayerInteract 射线命中后调用。
    /// 也可由 DeskInteract.onInteract 触发（自动绑定）。
    /// </summary>
    public void Interact()
    {
        if (!interactionEnabled)
            return;

        if (stage == 0)
        {
            Debug.Log(stage0Text);
            stage = 1;
            return;
        }

        if (stage == 1)
        {
            Debug.Log(stage1Text);
            stage = 2;
            return;
        }

        // stage >= 2: 发奖励并关闭交互
        GiveItem(rewardItemID);
        GameState.hasBattery = true;
        Debug.Log(stage2Text);
        interactionEnabled = false;
    }

    /// <summary>
    /// 占位：后续接入背包系统。
    /// </summary>
    public void GiveItem(string itemID)
    {
        Debug.Log($"[MultiStepInteract] GiveItem: {itemID}");
    }
}
