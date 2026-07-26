using UnityEngine;

public sealed class PlayerAbilityController : MonoBehaviour
{
    private const int SlotCount = 3;

    private readonly AbilityRuntimeSlot[] slots = new AbilityRuntimeSlot[SlotCount];

    private CharacterMovement movement;
    private Camera aimCamera;
    private bool initialized;

    public bool IsInitialized => initialized;

    private void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new AbilityRuntimeSlot
            {
                slotId = (AbilitySlotId)i,
                assignment = AbilitySlotAssignment.Empty((AbilitySlotId)i)
            };
        }
    }

    public void Initialize(AbilityLoadoutConfig loadoutConfig, CharacterMovement characterMovement, Camera abilityCamera)
    {
        movement = characterMovement != null ? characterMovement : GetComponent<CharacterMovement>();
        aimCamera = abilityCamera != null ? abilityCamera : Camera.main;

        for (int i = 0; i < slots.Length; i++)
        {
            AbilitySlotId slotId = (AbilitySlotId)i;
            AbilitySlotAssignment assignment = AbilitySlotAssignment.Empty(slotId);
            if (loadoutConfig != null)
            {
                loadoutConfig.TryGetAssignment(slotId, out assignment);
            }

            slots[i].slotId = slotId;
            slots[i].assignment = assignment;
            slots[i].cooldownRemaining = 0f;
            slots[i].execution = null;
        }

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            TickCooldown(ref slots[i], Time.deltaTime);
            TryActivateSlot(ref slots[i]);
        }
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            TickExecution(ref slots[i], Time.deltaTime);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].execution?.End();
            slots[i].execution = null;
        }
    }

    public AbilitySlotSnapshot GetSlotSnapshot(AbilitySlotId slotId)
    {
        AbilityRuntimeSlot slot = slots[(int)slotId];
        AbilityDefinition ability = slot.assignment.ability;
        return new AbilitySlotSnapshot(
            slotId,
            GetSlotKeyLabel(slotId),
            ability != null ? ability.AbilityName : "Empty",
            ability != null ? ability.Icon : null,
            ability != null,
            Mathf.Max(0f, slot.cooldownRemaining));
    }

    private void TryActivateSlot(ref AbilityRuntimeSlot slot)
    {
        AbilityDefinition ability = slot.assignment.ability;
        if (ability == null || slot.cooldownRemaining > 0f || slot.execution != null)
        {
            return;
        }

        if (!Input.GetKeyDown(GetActivationKey(slot.slotId)))
        {
            return;
        }

        var context = new AbilityActivationContext(transform, movement, aimCamera);
        if (!ability.TryCreateExecution(context, out AbilityExecution execution))
        {
            return;
        }

        slot.cooldownRemaining = ability.Cooldown;
        slot.execution = execution;
        slot.execution.Begin();
    }

    private static void TickCooldown(ref AbilityRuntimeSlot slot, float deltaTime)
    {
        if (slot.cooldownRemaining <= 0f)
        {
            slot.cooldownRemaining = 0f;
            return;
        }

        slot.cooldownRemaining = Mathf.Max(0f, slot.cooldownRemaining - deltaTime);
    }

    private static void TickExecution(ref AbilityRuntimeSlot slot, float deltaTime)
    {
        if (slot.execution == null)
        {
            return;
        }

        slot.execution.Tick(deltaTime);
        if (!slot.execution.IsComplete)
        {
            return;
        }

        slot.execution.End();
        slot.execution = null;
    }

    private static KeyCode GetActivationKey(AbilitySlotId slotId)
    {
        return slotId switch
        {
            AbilitySlotId.Slot1 => KeyCode.R,
            AbilitySlotId.Slot2 => KeyCode.F,
            AbilitySlotId.Slot3 => KeyCode.C,
            _ => KeyCode.None
        };
    }

    private static string GetSlotKeyLabel(AbilitySlotId slotId)
    {
        return GetActivationKey(slotId) switch
        {
            KeyCode.None => string.Empty,
            var key => key.ToString().ToUpperInvariant()
        };
    }

    private struct AbilityRuntimeSlot
    {
        public AbilitySlotId slotId;
        public AbilitySlotAssignment assignment;
        public float cooldownRemaining;
        public AbilityExecution execution;
    }
}

public readonly struct AbilitySlotSnapshot
{
    public AbilitySlotSnapshot(
        AbilitySlotId slotId,
        string keyLabel,
        string abilityName,
        Sprite icon,
        bool hasAssignedAbility,
        float cooldownRemaining)
    {
        SlotId = slotId;
        KeyLabel = keyLabel;
        AbilityName = abilityName;
        Icon = icon;
        HasAssignedAbility = hasAssignedAbility;
        CooldownRemaining = cooldownRemaining;
    }

    public AbilitySlotId SlotId { get; }
    public string KeyLabel { get; }
    public string AbilityName { get; }
    public Sprite Icon { get; }
    public bool HasAssignedAbility { get; }
    public float CooldownRemaining { get; }
    public bool IsOnCooldown => CooldownRemaining > 0f;
}
