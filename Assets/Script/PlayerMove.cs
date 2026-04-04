using UnityEngine;



public class PlayerMove : MonoBehaviour

{

    public float speed = 5f;

    [Tooltip("按住 Shift 时相对基础速度的倍数")]
    [SerializeField] float sprintMultiplier = 1.65f;

    public float gravity = -9.8f;



    [Tooltip("鼠标左右转动角色的速度")]

    [SerializeField] float mouseSensitivityX = 2f;

    [Tooltip("Animator 里用于接收移动速度的参数名")]
    [SerializeField] string speedParameter = "Speed";

    private CharacterController controller;
    private Animator animator;

    private Vector3 velocity;



    void Start()

    {

        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

    }



    void Update()

    {

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX;

        transform.Rotate(0f, mouseX, 0f);



        float h = Input.GetAxis("Horizontal");

        float v = Input.GetAxis("Vertical");



        Vector3 forward = transform.forward;

        forward.y = 0f;

        forward.Normalize();

        Vector3 right = transform.right;

        right.y = 0f;

        right.Normalize();



        Vector3 move = forward * v + right * h;
        float moveMagnitude = move.magnitude;

        if (move.sqrMagnitude > 1f)

            move.Normalize();



        bool sprint = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            && moveMagnitude > 0.01f;
        float moveSpeed = speed * (sprint ? sprintMultiplier : 1f);

        if (animator != null)
            animator.SetFloat(speedParameter, moveMagnitude * (sprint ? sprintMultiplier : 1f));

        controller.Move(move * moveSpeed * Time.deltaTime);



        if (controller.isGrounded && velocity.y < 0)

        {

            velocity.y = -2f;

        }



        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);

    }

}


