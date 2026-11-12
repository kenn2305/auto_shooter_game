using System;
using UnityEngine;

/// <summary>
/// One ultimate form of a weapon. The max level is the list of these forms: the player picks one
/// after the last normal level. Stat bonuses and rules stack with all normal levels, and an empty
/// Attack keeps the Attack of the last normal level.
/// </summary>
[Serializable]
public sealed class WeaponEvolutionData : WeaponLevelData
{
    [Header("Form Identity")]
    [Tooltip("Stable ID, unique inside this weapon.")]
    public string evolutionID;
    public string displayName;

    [Tooltip("Optional card icon. The weapon icon is used when empty.")]
    public Sprite icon;

    public bool IsConfiguredFor(WeaponLevelData lastNormalLevel)
    {
        WeaponAttackDefinition resolvedAttack = attack != null ? attack : lastNormalLevel?.attack;
        return !string.IsNullOrWhiteSpace(evolutionID) && resolvedAttack != null && resolvedAttack.attackPrefab != null;
    }

    /// <summary>
    /// Builds the level used at runtime without changing the shared asset data.
    /// </summary>
    public WeaponEvolutionData CreateRuntimeForm(WeaponLevelData lastNormalLevel)
    {
        return new WeaponEvolutionData
        {
            description = description,
            statBonuses = statBonuses,
            statRestrictions = statRestrictions,
            attack = attack != null ? attack : lastNormalLevel?.attack,
            triggeredEffects = triggeredEffects,
            evolutionID = evolutionID,
            displayName = displayName,
            icon = icon,
        };
    }
}
