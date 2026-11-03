using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class LevelUpWeaponOfferDefinition
{
    [Tooltip("Stable identity and level data. Existing weapons are matched by weaponID.")]
    public WeaponData weaponData;

    [Tooltip("Optional custom prefab used when acquiring this weapon. If empty, a runtime controller is created from Weapon Data.")]
    public WeaponController acquisitionPrefab;
    [Min(0f)] public float offerWeight = 1f;
    [Min(1)] public int minimumPlayerLevel = 2;

    public WeaponData ResolvedData => weaponData != null ? weaponData : acquisitionPrefab?.Data;
    public string StableId =>
        ResolvedData == null
            ? string.Empty
            : !string.IsNullOrWhiteSpace(ResolvedData.weaponID) ? ResolvedData.weaponID : ResolvedData.name;
}

[Serializable]
public sealed class LevelUpItemOfferDefinition
{
    public LevelUpItemData itemData;

    [Tooltip("Optional custom prefab. If empty, a runtime Item Controller is created from Item Data.")]
    public LevelUpItemController acquisitionPrefab;
    [Min(0f)] public float offerWeight = 1f;
    [Min(1)] public int minimumPlayerLevel = 2;

    public LevelUpItemData ResolvedData => itemData != null ? itemData : acquisitionPrefab?.Data;
    public string StableId =>
        ResolvedData == null
            ? string.Empty : !string.IsNullOrWhiteSpace(ResolvedData.itemID) ? ResolvedData.itemID : ResolvedData.name;
}

[Serializable]
public sealed class LevelUpStatDefinition
{
    public string statID;
    public string displayName;
    [TextArea] public string description;

    public Sprite icon;

    [Tooltip("Relative weight among other eligible Stat packages.")]
    [Min(0f)] public float offerWeight = 1f;
    [Min(1)] public int minimumPlayerLevel = 2;

    [Tooltip("0 means unlimited selections during a run.")]
    [Min(0)] public int maximumSelections;

    public List<CombatStatBonus> bonuses = new();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(statID) && bonuses != null && bonuses.Count > 0;

    public static LevelUpStatDefinition CreateFallback(
        string id,
        string title,
        string text,
        CombatStatType stat,
        float value
    )
    {
        return new LevelUpStatDefinition
        {
            statID = id,
            displayName = title,
            description = text,
            offerWeight = 1f,
            minimumPlayerLevel = 1,
            maximumSelections = 0,
            bonuses = new List<CombatStatBonus> { new(stat, value) },
        };
    }
}

[Serializable]
public sealed class LevelUpDraftPhase
{
    [Min(1)] public int minPlayerLevel = 2;

    [Header("Category Weights")]
    [Min(0f)] public float newEquipmentWeight = 50f;
    [Min(0f)] public float ownedUpgradeWeight = 40f;
    [Min(0f)] public float statPackageWeight = 10f;

    [Header("Fairness Guarantees")]
    public bool guaranteeNewEquipmentWhenSlotAvailable = true;
    public bool guaranteeOwnedUpgradeWhenAvailable = true;

    public static LevelUpDraftPhase CreateFallback()
    {
        return new LevelUpDraftPhase
        {
            minPlayerLevel = 1,
            newEquipmentWeight = 35f,
            ownedUpgradeWeight = 50f,
            statPackageWeight = 15f,
            guaranteeNewEquipmentWhenSlotAvailable = true,
            guaranteeOwnedUpgradeWhenAvailable = true,
        };
    }
}
