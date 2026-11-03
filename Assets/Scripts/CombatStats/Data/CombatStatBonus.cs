using System;
using UnityEngine;

[Serializable]
public struct CombatStatBonus
{
    [Tooltip("Stat that receives this bonus. Values are percentage points: 30 means +30%.")]
    public CombatStatType stat;

    [Tooltip("Percentage points added to the selected stat. Negative values are allowed.")]
    public float value;

    public CombatStatBonus(CombatStatType stat, float value)
    {
        this.stat = stat;
        this.value = value;
    }
}
