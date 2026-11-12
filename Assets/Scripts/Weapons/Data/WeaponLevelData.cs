using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponLevelData
{
    [TextArea] public string description;

    [Header("Bonus Stats Granted At This Level")]
    [Tooltip(
        "Bonuses granted to the owner when this level is reached. "
            + "They stack with bonuses from all earlier levels of this weapon. "
            + "Value 30 means +30% of the base stat. Max Health adds a flat number."
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
