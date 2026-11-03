using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WeaponLevelData
{
    [TextArea]
    public string description;

    [Header("Attack Speed")]
    [Tooltip("Time in seconds between attacks")]
    [Min(0.01f)]
    public float attackInterval = 1f;

    [Tooltip("Minimum time between attacks. Set to -1 for no limit.")]
    [Min(-1f)]
    public float minimumAttackInterval = 0.05f;

    [Header("Attack Modifiers")]
    [Tooltip("Damage multiplier of current level")]
    [Min(0.01f)]
    public float damage;

    [Tooltip("Value of hitbox size multiplier, 1 = default size")]
    [Min(0f)]
    public float areaMultiplier = 1f;

    [Header("Bonus Stats Granted At This Level")]
    [Tooltip(
        "Bonuses granted to the owner when this level is reached. "
            + "They stack with bonuses from all earlier levels of this weapon. "
            + "Value 30 means +30 percentage points."
    )]
    public List<CombatStatBonus> statBonuses = new();

    [Header("Player Stat Rules Unlocked At This Level")]
    [Tooltip(
        "Global player rules unlocked at this level and kept at later levels. "
            + "They can block or convert stats from other sources while allowing this weapon's own stats."
    )]
    public List<CombatStatRestriction> statRestrictions = new();

    [Header("Attack")]
    [Tooltip(
        "Complete attack profile used by this level. Assign a different profile to change "
            + "the attack type, prefab, animation, projectile trajectory, impact behaviour, or other mechanics."
    )]
    public WeaponAttackDefinition attack;

    [Header("Triggered Effects")]
    public List<WeaponTriggeredEffectData> triggeredEffects = new();
}
