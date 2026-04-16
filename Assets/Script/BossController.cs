using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum BossState
{
    Invisible,
    Revealed,
    Stunned
}

[DisallowMultipleComponent]
public class BossController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("可选：用于可见性判定中心点；不填则使用本物体位置")]
    [SerializeField] Transform bossCenter;
    [Tooltip("不填则自动抓取子物体 Renderer")]
    [SerializeField] Renderer[] renderersToToggle;
    [Tooltip("受眩晕影响要禁用的 AI 组件（例如 BossPatrol）")]
    [SerializeField] MonoBehaviour[] aiBehavioursToDisable;
    [Tooltip("可选：Boss 使用 NavMeshAgent 时自动控制停走")]
    [SerializeField] NavMeshAgent navAgent;

    [Header("State")]
    [Tooltip("测试开关：关闭后 Boss 始终可见，不再执行隐身")]
    [SerializeField] bool invisibilityEnabled = false;
    [SerializeField] BossState currentState = BossState.Revealed;

    Coroutine _stunRoutine;

    public BossState CurrentState => currentState;
    public Transform Center => bossCenter != null ? bossCenter : transform;

    void Awake()
    {
        if ((renderersToToggle == null || renderersToToggle.Length == 0))
            renderersToToggle = GetComponentsInChildren<Renderer>(true);
        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (invisibilityEnabled)
        {
            SetInvisible();
            currentState = BossState.Invisible;
        }
        else
        {
            SetVisible();
            currentState = BossState.Revealed;
        }
    }

    public void RevealAndStun(float stunDuration)
    {
        if (_stunRoutine != null)
            StopCoroutine(_stunRoutine);
        _stunRoutine = StartCoroutine(RevealAndStunRoutine(stunDuration));
    }

    IEnumerator RevealAndStunRoutine(float stunDuration)
    {
        SetVisible();
        currentState = BossState.Revealed;

        DisableAIForStun();
        currentState = BossState.Stunned;

        yield return new WaitForSeconds(Mathf.Max(0f, stunDuration));

        EnableAIAfterStun();
        if (invisibilityEnabled)
        {
            SetInvisible();
            currentState = BossState.Invisible;
        }
        else
        {
            SetVisible();
            currentState = BossState.Revealed;
        }
        _stunRoutine = null;
    }

    public void SetInvisible()
    {
        SetRenderersEnabled(false);
    }

    public void SetVisible()
    {
        SetRenderersEnabled(true);
    }

    void SetRenderersEnabled(bool enabled)
    {
        if (renderersToToggle == null)
            return;
        foreach (Renderer r in renderersToToggle)
        {
            if (r != null)
                r.enabled = enabled;
        }
    }

    void DisableAIForStun()
    {
        if (aiBehavioursToDisable != null)
        {
            foreach (MonoBehaviour ai in aiBehavioursToDisable)
            {
                if (ai != null)
                    ai.enabled = false;
            }
        }

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
    }

    void EnableAIAfterStun()
    {
        if (aiBehavioursToDisable != null)
        {
            foreach (MonoBehaviour ai in aiBehavioursToDisable)
            {
                if (ai != null)
                    ai.enabled = true;
            }
        }

        if (navAgent != null)
            navAgent.isStopped = false;
    }
}
