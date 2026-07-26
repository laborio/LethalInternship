// Shared follow camera that can run in third-person or top-down mode.
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public enum CameraMode
    {
        ThirdPerson,
        TopDown
    }

    [Header("Mode")]
    [SerializeField] private CameraMode mode = CameraMode.ThirdPerson;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform yawAlignmentTarget;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Third Person")]
    [SerializeField] private float fixedPitch = 18f;
    [SerializeField] private bool enableWheelZoomInput = false;
    [SerializeField] private float minDistance = 2.5f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float zoomStep = 3.5f;
    [SerializeField] private float defaultDistance = 6f;
    [SerializeField] private float zoomSmoothTime = 0.08f;

    [Header("Follow")]
    [SerializeField] private float pivotSmoothTime = 0.06f;
    [SerializeField] private float rotationSmoothSpeed = 14f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private float collisionBuffer = 0.1f;
    [SerializeField] private float collisionInSmoothTime = 0.04f;
    [SerializeField] private float collisionOutSmoothTime = 0.12f;

    [Header("Auto Recenter")]
    [SerializeField] private bool recenterBehindOnMove = true;
    [SerializeField] private bool recenterBehindOnTurn = true;
    [SerializeField] private float moveRecenterSpeed = 260f;
    [SerializeField] private float turnYawDeltaThreshold = 0.1f;
    [SerializeField] private float fallbackMoveSpeedThreshold = 0.15f;

    [Header("Top Down")]
    [SerializeField] private Vector3 topDownOffset = new Vector3(0f, 45f, -21f);
    [SerializeField] private Vector3 topDownRotationEuler = new Vector3(65f, 0f, 0f);
    [SerializeField] private float topDownFollowSmoothTime = 0.2f;

    private float yaw;
    private float targetDistance;
    private float zoomDistance;
    private float zoomVelocity;
    private float currentDistance;
    private float distanceVelocity;
    private Vector3 smoothedPivotPosition;
    private Vector3 pivotVelocity;
    private bool initialized;
    private CharacterMovement cachedMovement;
    private Vector3 lastTargetPosition;
    private float lastAlignmentYaw;
    private bool hasLastAlignmentYaw;
    private Vector3 topDownVelocity;
    private CameraMode lastAppliedMode;

    private const int MaxSphereHits = 8;
    private readonly RaycastHit[] sphereHits = new RaycastHit[MaxSphereHits];

    public Transform CameraTransform => transform;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        cachedMovement = null;
        lastTargetPosition = target != null ? target.position : Vector3.zero;
        hasLastAlignmentYaw = false;
        topDownVelocity = Vector3.zero;

        if (yawAlignmentTarget == null && target != null)
        {
            yawAlignmentTarget = target;
        }

        InitializeFromCurrentPose(forceSnap: true);
    }

    public void SetYawAlignmentTarget(Transform newYawAlignmentTarget)
    {
        yawAlignmentTarget = newYawAlignmentTarget;
        cachedMovement = null;
        hasLastAlignmentYaw = false;
    }

    private void Start()
    {
        InitializeFromCurrentPose(forceSnap: true);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (!initialized)
        {
            InitializeFromCurrentPose(forceSnap: true);
        }

        if (lastAppliedMode != mode)
        {
            InitializeFromCurrentPose(forceSnap: true);
        }

        if (mode == CameraMode.TopDown)
        {
            UpdateTopDownCamera();
            return;
        }

        UpdateZoomInput();

        Transform recenterTarget = yawAlignmentTarget != null ? yawAlignmentTarget : target;
        float alignmentYaw = recenterTarget != null ? recenterTarget.eulerAngles.y : yaw;
        float yawDeltaThisFrame = ConsumeAlignmentYawDelta(alignmentYaw);

        bool shouldRecenterFromMove = recenterBehindOnMove && IsTargetMovingForRecenter();
        bool shouldRecenterFromTurn = recenterBehindOnTurn && yawDeltaThisFrame >= turnYawDeltaThreshold;
        if (shouldRecenterFromMove || shouldRecenterFromTurn)
        {
            yaw = Mathf.MoveTowardsAngle(yaw, alignmentYaw, moveRecenterSpeed * Time.deltaTime);
        }

        Vector3 targetPivotPosition = target.position + pivotOffset;
        smoothedPivotPosition = Vector3.SmoothDamp(
            smoothedPivotPosition,
            targetPivotPosition,
            ref pivotVelocity,
            pivotSmoothTime);

        Quaternion desiredRotation = Quaternion.Euler(fixedPitch, yaw, 0f);
        zoomDistance = Mathf.SmoothDamp(zoomDistance, targetDistance, ref zoomVelocity, zoomSmoothTime);

        float collisionLimitedDistance = ResolveCollisionDistance(smoothedPivotPosition, desiredRotation, zoomDistance);
        float distanceSmoothTime = collisionLimitedDistance < currentDistance ? collisionInSmoothTime : collisionOutSmoothTime;
        currentDistance = Mathf.SmoothDamp(currentDistance, collisionLimitedDistance, ref distanceVelocity, distanceSmoothTime);

        Vector3 desiredPosition = smoothedPivotPosition - (desiredRotation * Vector3.forward * currentDistance);
        transform.position = desiredPosition;
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
    }

    private void UpdateTopDownCamera()
    {
        Vector3 desiredPosition = target.position + topDownOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref topDownVelocity,
            topDownFollowSmoothTime);
        transform.rotation = Quaternion.Euler(topDownRotationEuler);
    }

    private void UpdateZoomInput()
    {
        if (!enableWheelZoomInput)
        {
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.0001f)
        {
            return;
        }

        targetDistance -= scroll * zoomStep;
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
    }

    private float ResolveCollisionDistance(Vector3 pivotPosition, Quaternion orbitRotation, float requestedDistance)
    {
        Vector3 castDirection = orbitRotation * Vector3.back;
        float clampedRequestedDistance = Mathf.Clamp(requestedDistance, minDistance, maxDistance);

        int hitCount = Physics.SphereCastNonAlloc(
            pivotPosition,
            collisionRadius,
            castDirection,
            sphereHits,
            clampedRequestedDistance + collisionBuffer,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = sphereHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;
            if (target != null && (hitTransform == target || hitTransform.IsChildOf(target)))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
            }
        }

        if (nearestDistance == float.MaxValue)
        {
            return clampedRequestedDistance;
        }

        float safeDistance = nearestDistance - collisionBuffer;
        return Mathf.Clamp(safeDistance, minDistance, clampedRequestedDistance);
    }

    private void InitializeFromCurrentPose(bool forceSnap)
    {
        if (target == null)
        {
            initialized = false;
            return;
        }

        lastAppliedMode = mode;
        topDownVelocity = Vector3.zero;

        if (mode == CameraMode.TopDown)
        {
            if (forceSnap || !initialized)
            {
                transform.position = target.position + topDownOffset;
            }

            transform.rotation = Quaternion.Euler(topDownRotationEuler);
            lastTargetPosition = target.position;
            initialized = true;
            return;
        }

        Vector3 pivotPosition = target.position + pivotOffset;
        Transform recenterTarget = yawAlignmentTarget != null ? yawAlignmentTarget : target;
        yaw = recenterTarget != null ? recenterTarget.eulerAngles.y : transform.eulerAngles.y;

        float measuredDistance = Vector3.Distance(transform.position, pivotPosition);
        float initialDistance = measuredDistance > 0.01f ? measuredDistance : defaultDistance;
        targetDistance = Mathf.Clamp(initialDistance, minDistance, maxDistance);
        zoomDistance = targetDistance;
        zoomVelocity = 0f;
        currentDistance = targetDistance;
        distanceVelocity = 0f;

        if (forceSnap || !initialized)
        {
            smoothedPivotPosition = pivotPosition;
            pivotVelocity = Vector3.zero;
        }

        lastTargetPosition = target.position;
        if (recenterTarget != null)
        {
            lastAlignmentYaw = recenterTarget.eulerAngles.y;
            hasLastAlignmentYaw = true;
        }

        initialized = true;
    }

    private float ConsumeAlignmentYawDelta(float currentYaw)
    {
        if (!hasLastAlignmentYaw)
        {
            lastAlignmentYaw = currentYaw;
            hasLastAlignmentYaw = true;
            return 0f;
        }

        float delta = Mathf.Abs(Mathf.DeltaAngle(lastAlignmentYaw, currentYaw));
        lastAlignmentYaw = currentYaw;
        return delta;
    }

    private bool IsTargetMovingForRecenter()
    {
        Transform movementSource = yawAlignmentTarget != null ? yawAlignmentTarget : target;
        if (cachedMovement == null && movementSource != null)
        {
            cachedMovement = movementSource.GetComponent<CharacterMovement>();
            if (cachedMovement == null)
            {
                cachedMovement = movementSource.GetComponentInParent<CharacterMovement>();
            }
        }

        if (cachedMovement != null)
        {
            if (target != null)
            {
                lastTargetPosition = target.position;
            }

            return cachedMovement.IsMoving;
        }

        if (target == null)
        {
            return false;
        }

        Vector3 currentPosition = target.position;
        Vector3 planarDelta = currentPosition - lastTargetPosition;
        planarDelta.y = 0f;
        lastTargetPosition = currentPosition;

        float planarSpeed = planarDelta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        return planarSpeed > fallbackMoveSpeedThreshold;
    }
}
