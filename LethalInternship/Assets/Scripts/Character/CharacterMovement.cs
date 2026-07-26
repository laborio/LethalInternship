// Handles local character locomotion with clean ground movement, jumping, and wall jumps.
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 4.5f;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGraceTime = 0.12f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField, Range(0f, 20f)] private float airControl = 6f;
    [SerializeField] private float fallingVelocityThreshold = -1f;
    [SerializeField] private float groundProbeRadius = 0.2f;
    [SerializeField] private float groundProbeDistance = 0.25f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Wall Jump")]
    [SerializeField] private LayerMask wallMask = ~0;
    [SerializeField, Range(0f, 0.5f)] private float wallContactLinger = 0.15f;
    [SerializeField, Range(0f, 1f)] private float wallJumpCooldown = 0.2f;
    [SerializeField] private float wallJumpUpVelocity = 7.5f;
    [SerializeField] private float wallJumpHorizontalSpeed = 6f;
    [SerializeField, Range(0f, 1f)] private float wallSurfaceMaxUpDot = 0.2f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Input")]
    [SerializeField] private bool allowWASD = true;
    [SerializeField] private bool useCameraRelative = true;
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private NetworkObject networkObject;
    private bool hasNetworkObject;
    private float verticalVelocity;
    private float lastGroundedTime;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private Vector3 currentPlanarVelocity;
    private float wallContactTimer;
    private Vector3 wallNormal;
    private float wallJumpCooldownTimer;
    private int movementInputSuppressionCount;
    private int locomotionSuppressionCount;

    public Vector2 MoveInput { get; private set; }
    public Vector3 MoveDirection { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsWalking { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsFalling { get; private set; }
    public float VerticalVelocity => verticalVelocity;
    public Transform CameraTransform => cameraTransform;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        networkObject = GetComponent<NetworkObject>();
        hasNetworkObject = networkObject != null;
        lastGroundedTime = -999f;
    }

    private void Update()
    {
        if (hasNetworkObject && !networkObject.IsOwner)
        {
            return;
        }

        ReadMoveInput();
        UpdateMovement();
        UpdateStateFlags();
    }

    private void ReadMoveInput()
    {
        if (movementInputSuppressionCount > 0)
        {
            MoveInput = Vector2.zero;
            return;
        }

        bool right = Input.GetKey(KeyCode.D);
        bool left = Input.GetKey(KeyCode.Q) || (allowWASD && Input.GetKey(KeyCode.A));
        bool forward = Input.GetKey(KeyCode.Z) || (allowWASD && Input.GetKey(KeyCode.W));
        bool back = Input.GetKey(KeyCode.S);

        float x = (right ? 1f : 0f) + (left ? -1f : 0f);
        float y = (forward ? 1f : 0f) + (back ? -1f : 0f);

        MoveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    private void UpdateMovement()
    {
        if (locomotionSuppressionCount > 0)
        {
            MoveInput = Vector2.zero;
            MoveDirection = Vector3.zero;
            IsMoving = false;
            IsWalking = false;
            IsRunning = false;
            currentPlanarVelocity = Vector3.zero;
            wallContactTimer = Mathf.Max(wallContactTimer - Time.deltaTime, 0f);
            wallJumpCooldownTimer = Mathf.Max(wallJumpCooldownTimer - Time.deltaTime, 0f);
            IsGrounded = CheckGrounded();
            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
            }

            return;
        }

        bool inputSuppressed = movementInputSuppressionCount > 0;
        bool walkHeld = !inputSuppressed && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        bool jumpPressed = !inputSuppressed && Input.GetKeyDown(KeyCode.Space);
        float deltaTime = Time.deltaTime;
        Vector3 moveDir = GetMoveDirection(MoveInput);

        IsMoving = moveDir.sqrMagnitude > 0.01f;
        IsWalking = IsMoving && walkHeld;
        IsRunning = IsMoving && !walkHeld;
        MoveDirection = moveDir;

        wallContactTimer = Mathf.Max(wallContactTimer - deltaTime, 0f);
        wallJumpCooldownTimer = Mathf.Max(wallJumpCooldownTimer - deltaTime, 0f);

        bool wasGrounded = CheckGrounded();
        if (wasGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
            currentPlanarVelocity = moveDir * (IsWalking ? walkSpeed : runSpeed);
            coyoteTimer = groundedGraceTime;
        }
        else
        {
            coyoteTimer = Mathf.Max(coyoteTimer - deltaTime, 0f);
        }

        if (jumpPressed)
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer = Mathf.Max(jumpBufferTimer - deltaTime, 0f);
        }

        Vector3 desiredPlanarVelocity = moveDir * (IsMoving ? (IsWalking ? walkSpeed : runSpeed) : 0f);
        if (wasGrounded)
        {
            currentPlanarVelocity = desiredPlanarVelocity;
        }
        else if (desiredPlanarVelocity.sqrMagnitude > 0.0001f)
        {
            float airLerpFactor = Mathf.Clamp01(airControl * deltaTime);
            currentPlanarVelocity = Vector3.Lerp(currentPlanarVelocity, desiredPlanarVelocity, airLerpFactor);
        }

        bool bufferedJumpRequested = jumpBufferTimer > 0f;
        if (bufferedJumpRequested && coyoteTimer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            wallContactTimer = 0f;
        }
        else if (bufferedJumpRequested && CanWallJump(wasGrounded))
        {
            Vector3 planarIncoming = currentPlanarVelocity.sqrMagnitude > 0.0001f
                ? currentPlanarVelocity
                : transform.forward;
            planarIncoming.y = 0f;

            Vector3 bounceDirection = planarIncoming.sqrMagnitude > 0.0001f && wallNormal.sqrMagnitude > 0.0001f
                ? Vector3.Reflect(planarIncoming.normalized, wallNormal)
                : (wallNormal.sqrMagnitude > 0.0001f ? -wallNormal : -transform.forward);

            bounceDirection.y = 0f;
            if (bounceDirection.sqrMagnitude > 0.0001f)
            {
                bounceDirection.Normalize();
                currentPlanarVelocity = bounceDirection * wallJumpHorizontalSpeed;
                transform.rotation = Quaternion.LookRotation(bounceDirection, Vector3.up);
            }

            verticalVelocity = wallJumpUpVelocity;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            wallContactTimer = 0f;
            wallJumpCooldownTimer = wallJumpCooldown;
        }

        verticalVelocity += gravity * deltaTime;

        Vector3 velocity = currentPlanarVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * deltaTime);

        IsGrounded = CheckGrounded();
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
        }

        Vector3 rotationDirection = moveDir.sqrMagnitude > 0.001f ? moveDir : currentPlanarVelocity;
        rotationDirection.y = 0f;
        if (rotationDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(rotationDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
        }
    }

    private void UpdateStateFlags()
    {
        IsJumping = !IsGrounded && verticalVelocity > 0.1f;

        bool recentlyGrounded = Time.time - lastGroundedTime <= groundedGraceTime;
        IsFalling = !recentlyGrounded && verticalVelocity <= fallingVelocityThreshold;
    }

    public void FaceDirection(Vector3 direction, bool instant = true, float customRotationSpeed = -1f)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        float speed = customRotationSpeed > 0f ? customRotationSpeed : rotationSpeed;
        transform.rotation = instant
            ? targetRotation
            : Quaternion.Slerp(transform.rotation, targetRotation, speed * Time.deltaTime);
    }

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    public void SetMovementInputSuppressed(bool suppressed)
    {
        if (suppressed)
        {
            movementInputSuppressionCount++;
            return;
        }

        movementInputSuppressionCount = Mathf.Max(0, movementInputSuppressionCount - 1);
    }

    public void SetLocomotionSuppressed(bool suppressed)
    {
        if (suppressed)
        {
            locomotionSuppressionCount++;
            MoveInput = Vector2.zero;
            MoveDirection = Vector3.zero;
            currentPlanarVelocity = Vector3.zero;
            jumpBufferTimer = 0f;
            return;
        }

        locomotionSuppressionCount = Mathf.Max(0, locomotionSuppressionCount - 1);
    }

    public void MoveWithController(Vector3 worldDisplacement)
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        controller.Move(worldDisplacement);
        IsGrounded = CheckGrounded();
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
        }
    }

    private bool CheckGrounded()
    {
        if (controller == null)
        {
            return false;
        }

        if (controller.isGrounded)
        {
            return true;
        }

        if (groundProbeRadius <= 0f || groundProbeDistance <= 0f)
        {
            return false;
        }

        Bounds bounds = controller.bounds;
        Vector3 origin = bounds.center;
        origin.y = bounds.min.y + groundProbeRadius + 0.01f;
        float castDistance = groundProbeDistance + 0.02f;
        int mask = groundMask.value != 0 ? groundMask.value : Physics.DefaultRaycastLayers;

        return Physics.SphereCast(
            origin,
            groundProbeRadius,
            Vector3.down,
            out _,
            castDistance,
            mask,
            QueryTriggerInteraction.Ignore);
    }

    private bool CanWallJump(bool wasGrounded)
    {
        return !wasGrounded
            && wallContactTimer > 0f
            && wallJumpCooldownTimer <= 0f
            && wallJumpUpVelocity > 0f
            && wallJumpHorizontalSpeed > 0f;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (controller == null || !controller.enabled || wallContactLinger <= 0f || IsGrounded)
        {
            return;
        }

        if (!IsLayerInMask(hit.collider.gameObject.layer, wallMask))
        {
            return;
        }

        Vector3 contactNormal = hit.normal;
        if (Mathf.Abs(contactNormal.y) > wallSurfaceMaxUpDot)
        {
            return;
        }

        contactNormal.y = 0f;
        if (contactNormal.sqrMagnitude < 0.0001f)
        {
            return;
        }

        wallNormal = contactNormal.normalized;
        wallContactTimer = wallContactLinger;
    }

    private void OnDisable()
    {
        verticalVelocity = 0f;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        currentPlanarVelocity = Vector3.zero;
        wallContactTimer = 0f;
        wallJumpCooldownTimer = 0f;
        wallNormal = Vector3.zero;
        movementInputSuppressionCount = 0;
        locomotionSuppressionCount = 0;
        IsMoving = false;
        IsWalking = false;
        IsRunning = false;
        IsGrounded = false;
        IsJumping = false;
        IsFalling = false;
    }

    private Vector3 GetMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (useCameraRelative && cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        Vector3 direction = right * input.x + forward * input.y;
        direction.Normalize();
        return direction;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
