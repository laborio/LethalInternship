using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Lethal Internship/Abilities/Loadout Config", fileName = "AbilityLoadoutConfig")]
public sealed class AbilityLoadoutConfig : ScriptableObject
{
    [SerializeField] private AbilitySlotAssignment[] slotAssignments = Array.Empty<AbilitySlotAssignment>();

    public bool TryGetAssignment(AbilitySlotId slotId, out AbilitySlotAssignment assignment)
    {
        if (slotAssignments != null)
        {
            for (int i = 0; i < slotAssignments.Length; i++)
            {
                if (slotAssignments[i].slotId != slotId)
                {
                    continue;
                }

                assignment = slotAssignments[i];
                return true;
            }
        }

        assignment = AbilitySlotAssignment.Empty(slotId);
        return false;
    }
}

public enum AbilitySlotId
{
    Slot1 = 0,
    Slot2 = 1,
    Slot3 = 2
}

[Serializable]
public struct AbilitySlotAssignment
{
    public AbilitySlotId slotId;
    public AbilityDefinition ability;

    public static AbilitySlotAssignment Empty(AbilitySlotId slotId)
    {
        return new AbilitySlotAssignment
        {
            slotId = slotId,
            ability = null
        };
    }
}
