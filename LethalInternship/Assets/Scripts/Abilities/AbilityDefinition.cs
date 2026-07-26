using UnityEngine;

public abstract class AbilityDefinition : ScriptableObject
{
    [SerializeField] private string abilityName = "Ability";
    [SerializeField] private Sprite icon;
    [SerializeField, Min(0f)] private float cooldown = 1f;

    public string AbilityName => string.IsNullOrWhiteSpace(abilityName) ? name : abilityName;
    public Sprite Icon => icon;
    public float Cooldown => cooldown;

    public bool TryCreateExecution(AbilityActivationContext context, out AbilityExecution execution)
    {
        execution = null;
        if (context == null)
        {
            return false;
        }

        execution = CreateExecution(context);
        return execution != null;
    }

    protected abstract AbilityExecution CreateExecution(AbilityActivationContext context);
}

public sealed class AbilityActivationContext
{
    public AbilityActivationContext(Transform ownerTransform, CharacterMovement movement, Camera aimCamera)
    {
        OwnerTransform = ownerTransform;
        Movement = movement;
        AimCamera = aimCamera;
    }

    public Transform OwnerTransform { get; }
    public CharacterMovement Movement { get; }
    public Camera AimCamera { get; }
}

public abstract class AbilityExecution
{
    protected AbilityExecution(AbilityActivationContext context)
    {
        Context = context;
    }

    protected AbilityActivationContext Context { get; }
    public abstract bool IsComplete { get; }

    public virtual void Begin()
    {
    }

    public abstract void Tick(float deltaTime);

    public virtual void End()
    {
    }
}
