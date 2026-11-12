using UnityEngine;

public enum LevelUpChoiceKind
{
    NewWeapon = 0,
    WeaponUpgrade = 1,
    NewItem = 2,
    ItemUpgrade = 3,
    StatPackage = 4,
    WeaponEvolution = 5,
}

public sealed class LevelUpChoice
{
    public LevelUpChoiceKind Kind { get; }
    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public Sprite Icon { get; }

    public LevelUpWeaponOfferDefinition WeaponDefinition { get; }
    public WeaponController OwnedWeapon { get; }
    public LevelUpItemOfferDefinition ItemDefinition { get; }
    public LevelUpItemController OwnedItem { get; }
    public LevelUpStatDefinition StatDefinition { get; }
    public WeaponEvolutionData Evolution { get; }

    private LevelUpChoice(
        LevelUpChoiceKind kind,
        string key,
        string title,
        string description,
        Sprite icon,
        LevelUpWeaponOfferDefinition weaponDefinition = null,
        WeaponController ownedWeapon = null,
        LevelUpItemOfferDefinition itemDefinition = null,
        LevelUpItemController ownedItem = null,
        LevelUpStatDefinition statDefinition = null,
        WeaponEvolutionData evolution = null
    )
    {
        Kind = kind;
        Key = key;
        Title = title;
        Description = description;
        Icon = icon;
        WeaponDefinition = weaponDefinition;
        OwnedWeapon = ownedWeapon;
        ItemDefinition = itemDefinition;
        OwnedItem = ownedItem;
        StatDefinition = statDefinition;
        Evolution = evolution;
    }

    public static LevelUpChoice NewWeapon(LevelUpWeaponOfferDefinition definition)
    {
        WeaponData data = definition.ResolvedData;
        return new LevelUpChoice(
            LevelUpChoiceKind.NewWeapon,
            $"weapon:{definition.StableId}",
            $"New Weapon: {GetName(data?.displayName, data?.name)}",
            data?.GetLevel(0)?.description ?? "Acquire this weapon at Level 1.",
            data?.icon,
            weaponDefinition: definition
        );
    }

    public static LevelUpChoice UpgradeWeapon(LevelUpWeaponOfferDefinition definition, WeaponController controller)
    {
        WeaponData data = controller.Data;
        int targetIndex = Mathf.Min(controller.CurrentLevelIndex + 1, controller.MaximumLevel - 1);
        return new LevelUpChoice(
            LevelUpChoiceKind.WeaponUpgrade,
            $"weapon:{definition.StableId}",
            $"{GetName(data?.displayName, data?.name)} Lv. {targetIndex + 1}",
            data?.GetLevel(targetIndex)?.description ?? "Upgrade this weapon.",
            data?.icon,
            weaponDefinition: definition,
            ownedWeapon: controller
        );
    }

    public static LevelUpChoice EvolveWeapon(
        LevelUpWeaponOfferDefinition definition,
        WeaponController controller,
        WeaponEvolutionData evolution
    )
    {
        WeaponData data = controller.Data;
        return new LevelUpChoice(
            LevelUpChoiceKind.WeaponEvolution,
            $"evolution:{definition.StableId}:{evolution.evolutionID}",
            $"{GetName(data?.displayName, data?.name)} Lv. {controller.MaximumLevel}: "
                + GetName(evolution.displayName, evolution.evolutionID),
            evolution.description,
            evolution.icon != null ? evolution.icon : data?.icon,
            weaponDefinition: definition,
            ownedWeapon: controller,
            evolution: evolution
        );
    }

    public static LevelUpChoice NewItem(LevelUpItemOfferDefinition definition)
    {
        LevelUpItemData data = definition.ResolvedData;
        return new LevelUpChoice(
            LevelUpChoiceKind.NewItem,
            $"item:{definition.StableId}",
            $"New Item: {GetName(data?.displayName, data?.name)}",
            data?.GetLevel(0)?.description ?? "Acquire this Item at Level 1.",
            data?.icon,
            itemDefinition: definition
        );
    }

    public static LevelUpChoice UpgradeItem(LevelUpItemOfferDefinition definition, LevelUpItemController controller)
    {
        LevelUpItemData data = controller.Data;
        int targetIndex = Mathf.Min(controller.CurrentLevelIndex + 1, controller.MaximumLevel - 1);
        return new LevelUpChoice(
            LevelUpChoiceKind.ItemUpgrade,
            $"item:{definition.StableId}",
            $"{GetName(data?.displayName, data?.name)} Lv. {targetIndex + 1}",
            data?.GetLevel(targetIndex)?.description ?? "Upgrade this Item.",
            data?.icon,
            itemDefinition: definition,
            ownedItem: controller
        );
    }

    public static LevelUpChoice Stat(LevelUpStatDefinition definition)
    {
        return new LevelUpChoice(
            LevelUpChoiceKind.StatPackage,
            $"stat:{definition.statID}",
            GetName(definition.displayName, definition.statID),
            definition.description,
            definition.icon,
            statDefinition: definition
        );
    }

    private static string GetName(string preferred, string fallback)
    {
        return !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
    }
}
