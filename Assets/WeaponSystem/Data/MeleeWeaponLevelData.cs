using UnityEngine;
using System;

[Serializable]
public sealed class MeleeWeaponLevelData
{
    [TextArea]
    public string description;

    [Header("Attack Speed")]
    [Tooltip("Time in seconds between attacks")]
    [Min(0.01f)]
    public float attackInterval = 1f;

    [Tooltip("Limit of attacks speed, -1 = unlimited")]
    [Min(-1f)]

    public float minimumAttackInterval = 0.05f;

    [Header("Attack Modifers")]
    [Tooltip("Damage multiplier of current level")]
    [Min(0.01f)]
    public float damage;

    [Tooltip("Value of hitbox size multiplier, 1 = default size")]
    [Min(0f)]
    public float areaMultiplier = 1f;

    [Header("Attack")]
    public MeleeAttackDefinition attack;
}
