using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float speed = 5f;
    public float gravity = -9.8f;
    public float rotateSpeed = 10f;
    [SerializeField] float sprintMultiplier = 1.6f;
    [SerializeField] Transform cameraTransform;
    [Header("Animation")]
    [SerializeField] Animator animator;
    [SerializeField] string speedParameter = "Speed";
    [SerializeField] string rollTriggerParameter = "Roll";
    [Tooltip("与 Animator 里翻滚状态名一致（用于 CrossFade 备用）")]
    [SerializeField] string rollStateName = "roll";

    [Header("Roll")]
    [SerializeField] KeyCode rollKey = KeyCode.LeftControl;
    [SerializeField] float rollSpeed = 12f;
    [SerializeField] float rollDuration = 0.25f;
    [SerializeField] float rollCooldown = 0.7f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 rollDirection;
    private float rollTimer;
    private float nextRollTime;
    private float controlLockTimer;
    private bool rootMotionTemporarilyDisabled;
    private bool originalApplyRootMotion;

    bool IsRolling => rollTimer > 0f;
    bool IsControlLocked => controlLockTimer > 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator != null)
            originalApplyRootMotion = animator.applyRootMotion;
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (controlLockTimer > 0f)
            controlLockTimer -= Time.deltaTime;
        ApplyMovementLockState();

        float h = IsControlLocked ? 0f : Input.GetAxis("Horizontal");
        float v = IsControlLocked ? 0f : Input.GetAxis("Vertical");

        Vector3 move = BuildCameraRelativeMove(h, v);
        if (move.sqrMagnitude > 1f)
            move.Normalize();
        float moveMagnitude = move.magnitude;

        TryStartRoll(move);

        if (IsRolling)
        {
            controller.Move(rollDirection * rollSpeed * Time.deltaTime);
            rollTimer -= Time.deltaTime;
            UpdateAnimation(0f, false);
        }
        else
        {
            // 有输入才转向
            if (moveMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
            }

            // 按住 Shift 慢跑（加速）
            bool sprint = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                && move.sqrMagnitude > 0.01f;
            float currentSpeed = speed * (sprint ? sprintMultiplier : 1f);

            // 普通移动
            controller.Move(move * currentSpeed * Time.deltaTime);
            UpdateAnimation(moveMagnitude, sprint);
        }

        // 重力
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// 在这段时间内屏蔽移动/翻滚输入（例如开枪硬直），可叠加刷新更长时长。
    /// </summary>
    public void LockControls(float duration)
    {
        if (duration <= 0f)
            return;
        controlLockTimer = Mathf.Max(controlLockTimer, duration);
    }

    void ApplyMovementLockState()
    {
        if (animator == null)
            return;

        if (IsControlLocked)
        {
            // 锁位移时，强制移动参数归零，避免状态机仍输出走跑动作导致“滑步感”。
            animator.SetFloat(speedParameter, 0f);

            // 若动画开启了 Root Motion，会把角色往前带；锁位移时临时关闭它。
            if (animator.applyRootMotion)
            {
                originalApplyRootMotion = true;
                animator.applyRootMotion = false;
                rootMotionTemporarilyDisabled = true;
            }
        }
        else if (rootMotionTemporarilyDisabled)
        {
            animator.applyRootMotion = originalApplyRootMotion;
            rootMotionTemporarilyDisabled = false;
        }
    }

    void UpdateAnimation(float moveMagnitude, bool sprint)
    {
        if (animator == null)
            return;

        float animSpeed = moveMagnitude * (sprint ? sprintMultiplier : 1f);
        animator.SetFloat(speedParameter, animSpeed);
    }

    Vector3 BuildCameraRelativeMove(float h, float v)
    {
        if (cameraTransform == null)
            return new Vector3(h, 0f, v);

        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.0001f)
            camForward = Vector3.forward;
        camForward.Normalize();

        Vector3 camRight = cameraTransform.right;
        camRight.y = 0f;
        if (camRight.sqrMagnitude < 0.0001f)
            camRight = Vector3.right;
        camRight.Normalize();

        return camForward * v + camRight * h;
    }

    void TryStartRoll(Vector3 move)
    {
        if (!Input.GetKeyDown(rollKey))
            return;
        if (IsRolling || Time.time < nextRollTime)
            return;

        rollDirection = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
        rollDirection.y = 0f;
        rollDirection.Normalize();

        // 开始翻滚
        rollTimer = rollDuration;
        nextRollTime = Time.time + rollCooldown;
        FireRollAnimation();

        if (rollDirection.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rollDirection);
    }

    void FireRollAnimation()
    {
        if (animator == null)
            return;

        bool fired = false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name != rollTriggerParameter)
                continue;
            if (p.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(rollTriggerParameter);
                animator.SetTrigger(rollTriggerParameter);
                fired = true;
            }
            break;
        }

        if (!fired && !string.IsNullOrEmpty(rollStateName))
        {
            int hash = Animator.StringToHash(rollStateName);
            if (animator.HasState(0, hash))
                animator.CrossFade(hash, 0.08f, 0, 0f);
        }
    }
}