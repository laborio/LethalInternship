using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayUiManager : MonoBehaviour
{
    private static GameplayUiManager instance;

    private readonly SkillSlotWidget[] skillSlots = new SkillSlotWidget[3];

    private PlayerAbilityController boundAbilityController;
    private bool skillBarResolved;

    public static GameplayUiManager GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<GameplayUiManager>();
        if (instance != null)
        {
            return instance;
        }

        var managerObject = new GameObject(nameof(GameplayUiManager));
        instance = managerObject.AddComponent<GameplayUiManager>();
        return instance;
    }

    public void BindLocalPlayer(PlayerAbilityController abilityController)
    {
        boundAbilityController = abilityController;
        skillBarResolved = false;
    }

    public void ClearLocalPlayer(PlayerAbilityController abilityController)
    {
        if (boundAbilityController == abilityController)
        {
            boundAbilityController = null;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Update()
    {
        if (boundAbilityController == null || !boundAbilityController.IsInitialized)
        {
            return;
        }

        if (!skillBarResolved && !TryResolveSkillBar())
        {
            return;
        }

        for (int i = 0; i < skillSlots.Length; i++)
        {
            skillSlots[i]?.Render(boundAbilityController.GetSlotSnapshot((AbilitySlotId)i));
        }
    }

    private bool TryResolveSkillBar()
    {
        RectTransform skillBarRoot = FindSkillBarRoot();
        if (skillBarRoot == null)
        {
            return false;
        }

        skillSlots[0] = SkillSlotWidget.Create(skillBarRoot, "Skill_01", "Skill01");
        skillSlots[1] = SkillSlotWidget.Create(skillBarRoot, "Skill_02", "Skill02");
        skillSlots[2] = SkillSlotWidget.Create(skillBarRoot, "Skill_03", "Skill03");
        skillBarResolved = skillSlots[0] != null && skillSlots[1] != null && skillSlots[2] != null;
        return skillBarResolved;
    }

    private static RectTransform FindSkillBarRoot()
    {
        var rectTransforms = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform == null || rectTransform.name != "SkillBar")
            {
                continue;
            }

            return rectTransform;
        }

        return null;
    }

    private sealed class SkillSlotWidget
    {
        private readonly GameObject rootObject;
        private readonly Image iconImage;
        private readonly TMP_Text keyText;
        private readonly TMP_Text nameText;
        private readonly TMP_Text timerText;

        private SkillSlotWidget(GameObject rootObject, Image iconImage, TMP_Text keyText, TMP_Text nameText, TMP_Text timerText)
        {
            this.rootObject = rootObject;
            this.iconImage = iconImage;
            this.keyText = keyText;
            this.nameText = nameText;
            this.timerText = timerText;
        }

        public static SkillSlotWidget Create(Transform skillBarRoot, params string[] slotNames)
        {
            Transform slotTransform = FindNamedChild(skillBarRoot, slotNames);
            if (slotTransform == null)
            {
                return null;
            }

            Image icon = FindNamedChild(slotTransform, "Icon")?.GetComponent<Image>();
            TMP_Text key = FindNamedChild(slotTransform, "Txt_Key")?.GetComponent<TMP_Text>();
            TMP_Text name = FindNamedChild(slotTransform, "Txt_Name", "Txt_Key (1)")?.GetComponent<TMP_Text>();
            TMP_Text timer = FindNamedChild(slotTransform, "Txt_timer")?.GetComponent<TMP_Text>();

            if (icon == null || key == null || name == null || timer == null)
            {
                return null;
            }

            return new SkillSlotWidget(slotTransform.gameObject, icon, key, name, timer);
        }

        public void Render(AbilitySlotSnapshot slot)
        {
            if (rootObject != null)
            {
                rootObject.SetActive(true);
            }

            if (keyText != null)
            {
                keyText.text = slot.KeyLabel;
            }

            if (nameText != null)
            {
                nameText.text = slot.AbilityName;
            }

            bool showTimer = slot.HasAssignedAbility && slot.IsOnCooldown;
            if (iconImage != null)
            {
                iconImage.sprite = slot.Icon;
                iconImage.enabled = slot.HasAssignedAbility && !showTimer;
            }

            if (timerText != null)
            {
                timerText.gameObject.SetActive(showTimer);
                if (showTimer)
                {
                    timerText.text = slot.CooldownRemaining.ToString("0.0");
                }
            }
        }

        private static Transform FindNamedChild(Transform parent, params string[] names)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                Transform match = FindNamedDescendant(parent, names[i]);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindNamedDescendant(Transform parent, string targetName)
        {
            if (parent.name == targetName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Transform match = FindNamedDescendant(child, targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
