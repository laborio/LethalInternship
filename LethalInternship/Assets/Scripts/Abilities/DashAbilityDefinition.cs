using UnityEngine;

[CreateAssetMenu(menuName = "Lethal Internship/Abilities/Dash Ability", fileName = "DashAbility")]
public sealed class DashAbilityDefinition : AbilityDefinition
{
    [SerializeField, Min(0.1f)] private float dashDistance = 5f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.15f;
    [SerializeField] private LayerMask aimLayers = ~0;
    [SerializeField, Min(1f)] private float aimRayDistance = 500f;
    [SerializeField] private bool suppressMovementInputWhileDashing = true;

    protected override AbilityExecution CreateExecution(AbilityActivationContext context)
    {
        if (context.Movement == null || context.OwnerTransform == null)
        {
            return null;
        }

        Vector3 dashDirection = ResolveDashDirection(context);
        dashDirection.y = 0f;
        if (dashDirection.sqrMagnitude < 0.0001f)
        {
            return null;
        }

        dashDirection.Normalize();
        return new DashAbilityExecution(
            context,
            dashDirection,
            dashDistance,
            dashDuration,
            suppressMovementInputWhileDashing);
    }

    private Vector3 ResolveDashDirection(AbilityActivationContext context)
    {
        Camera aimCamera = context.AimCamera;
        if (aimCamera == null && context.Movement != null && context.Movement.CameraTransform != null)
        {
            aimCamera = context.Movement.CameraTransform.GetComponent<Camera>();
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (aimCamera == null)
        {
            return context.OwnerTransform != null ? context.OwnerTransform.forward : Vector3.zero;
        }

        Ray ray = aimCamera.ScreenPointToRay(Input.mousePosition);
        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, aimRayDistance, aimLayers, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
        }
        else
        {
            Transform ownerTransform = context.OwnerTransform;
            float planeHeight = ownerTransform != null ? ownerTransform.position.y : 0f;
            Plane plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
            if (!plane.Raycast(ray, out float enter))
            {
                return ownerTransform != null ? ownerTransform.forward : Vector3.zero;
            }

            targetPoint = ray.GetPoint(enter);
        }

        return targetPoint - context.OwnerTransform.position;
    }

    private sealed class DashAbilityExecution : AbilityExecution
    {
        private readonly Vector3 dashDirection;
        private readonly float dashDistance;
        private readonly float dashDuration;
        private readonly bool suppressMovementInput;

        private float elapsed;
        private float movedDistance;

        public DashAbilityExecution(
            AbilityActivationContext context,
            Vector3 dashDirection,
            float dashDistance,
            float dashDuration,
            bool suppressMovementInput) : base(context)
        {
            this.dashDirection = dashDirection;
            this.dashDistance = dashDistance;
            this.dashDuration = dashDuration;
            this.suppressMovementInput = suppressMovementInput;
        }

        public override bool IsComplete => elapsed >= dashDuration || movedDistance >= dashDistance;

        public override void Begin()
        {
            if (Context.Movement == null)
            {
                return;
            }

            if (suppressMovementInput)
            {
                Context.Movement.SetMovementInputSuppressed(true);
                Context.Movement.SetLocomotionSuppressed(true);
            }

            Context.Movement.FaceDirection(dashDirection, true);
        }

        public override void Tick(float deltaTime)
        {
            if (Context.Movement == null || IsComplete)
            {
                return;
            }

            elapsed += deltaTime;

            float normalizedProgress = Mathf.Clamp01(elapsed / dashDuration);
            float targetDistance = dashDistance * normalizedProgress;
            float stepDistance = Mathf.Max(0f, targetDistance - movedDistance);
            if (stepDistance <= 0f)
            {
                return;
            }

            movedDistance += stepDistance;
            Context.Movement.MoveWithController(dashDirection * stepDistance);
        }

        public override void End()
        {
            if (Context.Movement == null || !suppressMovementInput)
            {
                return;
            }

            Context.Movement.SetLocomotionSuppressed(false);
            Context.Movement.SetMovementInputSuppressed(false);
        }
    }
}
