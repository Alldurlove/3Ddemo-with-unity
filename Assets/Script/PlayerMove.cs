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

    bool IsRolling => rollTimer > 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

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