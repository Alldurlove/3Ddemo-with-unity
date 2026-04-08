using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Boss 在“未发现玩家”时的巡逻逻辑。
/// 一旦外部系统调用 SetPlayerDetected(true)，本脚本会停止巡逻移动。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class BossPatrol : MonoBehaviour
{
    public enum PatrolMode
    {
        Loop,
        PingPong
    }

    [Header("Patrol Path")]
    [Tooltip("按顺序巡逻的路径点（至少 2 个更自然）")]
    [SerializeField] List<Transform> patrolPoints = new List<Transform>();
    [SerializeField] PatrolMode patrolMode = PatrolMode.Loop;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 1.5f;
    [SerializeField] float chaseSpeed = 2.5f;
    [SerializeField] float arriveDistance = 0.2f;
    [SerializeField] float waitAtPointSeconds = 0.8f;
    [SerializeField] float angularSpeed = 360f;
    [SerializeField] float acceleration = 8f;
    [Header("Animation")]
    [SerializeField] Animator animator;
    [SerializeField] string speedParamName = "Speed";
    [Tooltip("true: 输出 0~1（速度/MoveSpeed）；false: 输出世界速度值")]
    [SerializeField] bool normalizeSpeedForAnimator = true;
    [Tooltip("归一化参考速度。<=0 时自动使用 max(moveSpeed, chaseSpeed)")]
    [SerializeField] float animatorNormalizeMaxSpeed = 0f;
    [SerializeField] float speedDampTime = 0.08f;
    [SerializeField] string attackTriggerParameter = "Attack";
    [Tooltip("当 Attack Trigger 不存在时，尝试直接 CrossFade 到该状态名")]
    [SerializeField] string attackStateName = "attack";

    [Header("State")]
    [Tooltip("true 表示已注意到玩家，开始追踪")]
    [SerializeField] bool playerDetected;
    [Header("Detect & Chase")]
    [SerializeField] bool detectPlayerByRadius = true;
    [SerializeField] Transform playerTarget;
    [SerializeField] float detectRadius = 8f;
    [Tooltip("丢失半径建议略大于 detectRadius，避免边界抖动")]
    [SerializeField] float loseRadius = 10f;
    [SerializeField] float chaseStoppingDistance = 1.2f;
    [Header("Attack")]
    [SerializeField] float attackRange = 2.1f;
    [SerializeField] float attackCooldown = 1.2f;
    [SerializeField] int attackDamage = 1;

    int _pointIndex;
    int _direction = 1; // PingPong 模式用：1 前进 / -1 后退
    float _waitTimer;
    NavMeshAgent _agent;
    int _speedParamHash;
    int _attackTriggerParamHash;
    float _nextAttackTime;

    public bool PlayerDetected => playerDetected;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        _agent.speed = moveSpeed;
        _agent.angularSpeed = angularSpeed;
        _agent.acceleration = acceleration;
        _agent.stoppingDistance = Mathf.Max(0.01f, arriveDistance);
        _agent.autoBraking = true;
        CacheAnimatorParamHash();
        if (playerTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerTarget = p.transform;
        }
    }

    void Update()
    {
        if (_agent == null)
            return;

        UpdateDetectionByRadius();

        if (playerDetected)
        {
            UpdateChase();
            return;
        }

        if (_agent.speed != moveSpeed)
            _agent.speed = moveSpeed;

        if (_agent.isStopped)
            _agent.isStopped = false;
        _agent.stoppingDistance = Mathf.Max(0.01f, arriveDistance);
        if (patrolPoints == null || patrolPoints.Count == 0)
            return;

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.deltaTime;
            _agent.isStopped = true;
            return;
        }
        _agent.isStopped = false;

        Transform target = patrolPoints[_pointIndex];
        if (target == null)
        {
            AdvancePointIndex();
            return;
        }

        if (!_agent.hasPath && !_agent.pathPending)
            _agent.SetDestination(target.position);
        else if (!_agent.pathPending && Vector3.Distance(_agent.destination, target.position) > 0.05f)
            _agent.SetDestination(target.position);

        bool arrived = !_agent.pathPending &&
                       (_agent.remainingDistance <= _agent.stoppingDistance + 0.02f) &&
                       (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f);
        if (arrived)
        {
            _waitTimer = waitAtPointSeconds;
            AdvancePointIndex();
            return;
        }
    }

    void UpdateDetectionByRadius()
    {
        if (!detectPlayerByRadius)
            return;
        if (playerTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerTarget = p.transform;
            else
                return;
        }

        float dist = Vector3.Distance(transform.position, playerTarget.position);
        if (!playerDetected && dist <= detectRadius)
            playerDetected = true;
        else if (playerDetected && dist > loseRadius)
            playerDetected = false;
    }

    void UpdateChase()
    {
        if (playerTarget == null)
        {
            playerDetected = false;
            if (!_agent.isStopped)
                _agent.isStopped = true;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        bool inAttackRange = distToPlayer <= attackRange;
        if (inAttackRange)
        {
            _agent.isStopped = true;
            Vector3 lookDir = playerTarget.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, angularSpeed * Time.deltaTime / 360f);
            }

            if (Time.time >= _nextAttackTime)
                PerformAttack();
            return;
        }

        if (_agent.isStopped)
            _agent.isStopped = false;
        if (_agent.speed != chaseSpeed)
            _agent.speed = chaseSpeed;
        _agent.stoppingDistance = Mathf.Max(0.05f, chaseStoppingDistance);
        _agent.SetDestination(playerTarget.position);
    }

    void LateUpdate()
    {
        UpdateAnimatorSpeed();
    }

    /// <summary>
    /// 外部检测逻辑调用：true=发现玩家并追踪，false=丢失玩家恢复巡逻。
    /// </summary>
    public void SetPlayerDetected(bool detected)
    {
        playerDetected = detected;
        if (_agent != null && !detected)
        {
            _agent.isStopped = false;
            _agent.speed = moveSpeed;
            _agent.stoppingDistance = Mathf.Max(0.01f, arriveDistance);
        }
    }

    /// <summary>
    /// 可在运行时重置巡逻到第一个点。
    /// </summary>
    public void ResetPatrol()
    {
        _pointIndex = 0;
        _direction = 1;
        _waitTimer = 0f;
        if (_agent != null)
        {
            _agent.isStopped = false;
            _agent.ResetPath();
            if (patrolPoints != null && patrolPoints.Count > 0 && patrolPoints[0] != null)
                _agent.SetDestination(patrolPoints[0].position);
        }
    }

    void AdvancePointIndex()
    {
        if (patrolPoints.Count <= 1)
            return;

        if (patrolMode == PatrolMode.Loop)
        {
            _pointIndex = (_pointIndex + 1) % patrolPoints.Count;
            return;
        }

        // PingPong
        _pointIndex += _direction;
        if (_pointIndex >= patrolPoints.Count)
        {
            _pointIndex = patrolPoints.Count - 2;
            _direction = -1;
        }
        else if (_pointIndex < 0)
        {
            _pointIndex = 1;
            _direction = 1;
        }
    }

    void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        chaseSpeed = Mathf.Max(0.01f, chaseSpeed);
        arriveDistance = Mathf.Max(0.01f, arriveDistance);
        waitAtPointSeconds = Mathf.Max(0f, waitAtPointSeconds);
        angularSpeed = Mathf.Max(1f, angularSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        speedDampTime = Mathf.Max(0f, speedDampTime);
        detectRadius = Mathf.Max(0.1f, detectRadius);
        loseRadius = Mathf.Max(detectRadius, loseRadius);
        chaseStoppingDistance = Mathf.Max(0.05f, chaseStoppingDistance);
        attackRange = Mathf.Max(0.1f, attackRange);
        attackCooldown = Mathf.Max(0.01f, attackCooldown);
        CacheAnimatorParamHash();
    }

    void CacheAnimatorParamHash()
    {
        _speedParamHash = string.IsNullOrWhiteSpace(speedParamName) ? 0 : Animator.StringToHash(speedParamName);
        _attackTriggerParamHash = string.IsNullOrWhiteSpace(attackTriggerParameter) ? 0 : Animator.StringToHash(attackTriggerParameter);
    }

    void UpdateAnimatorSpeed()
    {
        if (animator == null || _speedParamHash == 0)
            return;

        float speed = (_agent != null) ? _agent.velocity.magnitude : 0f;
        if (normalizeSpeedForAnimator)
        {
            // 刚切换目的地时 velocity 可能瞬间为 0，fallback 到 desiredVelocity 可减少动画掉 Idle。
            if (_agent != null && speed < 0.01f && !_agent.isStopped)
                speed = _agent.desiredVelocity.magnitude;

            float maxSpeed = animatorNormalizeMaxSpeed > 0.001f
                ? animatorNormalizeMaxSpeed
                : Mathf.Max(moveSpeed, chaseSpeed);
            speed = maxSpeed > 0.001f ? Mathf.Clamp01(speed / maxSpeed) : 0f;
        }

        animator.SetFloat(_speedParamHash, speed, speedDampTime, Time.deltaTime);
    }

    void PerformAttack()
    {
        _nextAttackTime = Time.time + Mathf.Max(0.01f, attackCooldown);
        FireAttackAnimation();

        if (playerTarget == null)
            return;

        // 兼容各种玩家受伤脚本：只要实现了 TakeDamage(int) 即可被调用。
        playerTarget.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        Debug.Log($"[BossPatrol] Boss attacked player for {attackDamage} damage.");
    }

    void FireAttackAnimation()
    {
        if (animator == null)
            return;

        bool fired = false;
        if (_attackTriggerParamHash != 0)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name != attackTriggerParameter)
                    continue;
                if (p.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.ResetTrigger(attackTriggerParameter);
                    animator.SetTrigger(attackTriggerParameter);
                    fired = true;
                }
                break;
            }
        }

        if (!fired && !string.IsNullOrEmpty(attackStateName))
        {
            int hash = Animator.StringToHash(attackStateName);
            if (animator.HasState(0, hash))
            {
                animator.CrossFade(hash, 0.05f, 0, 0f);
                fired = true;
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (patrolPoints == null || patrolPoints.Count == 0)
            return;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            Transform p = patrolPoints[i];
            if (p == null)
                continue;
            Gizmos.DrawSphere(p.position, 0.12f);

            Transform next = null;
            if (i < patrolPoints.Count - 1)
                next = patrolPoints[i + 1];
            else if (patrolMode == PatrolMode.Loop)
                next = patrolPoints[0];

            if (next != null)
                Gizmos.DrawLine(p.position, next.position);
        }

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, loseRadius);
    }
#endif
}
