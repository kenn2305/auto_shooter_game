using System;
using UnityEngine;

[Serializable]
public struct CombatStatBonus
{
    [Tooltip("Stat that receives this bonus.")]
    public CombatStatType stat;

    [Tooltip(
        "Percent of the character's base stat: 30 means +30% of base, and sources add up without compounding. "
            + "Critical Chance adds percentage points. Max Health adds a flat number. Negative values are allowed."
    )]
    public float value;

    public CombatStatBonus(CombatStatType stat, float value)
    {
        this.stat = stat;
        this.value = value;
    }
}
