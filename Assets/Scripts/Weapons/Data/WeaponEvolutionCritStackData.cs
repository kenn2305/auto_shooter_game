using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Passive shared by every evolution form of a weapon: kills grant Critical Chance for a limited time,
/// and Critical Chance above the cap becomes ATK.
/// </summary>
[Serializable]
public sealed class WeaponEvolutionCritStackData
{
    [Tooltip("Turns the passive on for every evolution form of this weapon.")]
    public bool enabled;

    [Min(0f)]
    [Tooltip("Critical Chance points gained per stack. Each kill by this weapon adds one stack.")]
    public float critPerKill = 1f;

    [Min(0.01f)]
    [Tooltip("Seconds until all stacks are lost. Every kill restarts this timer.")]
    public float stackDuration = 10f;

    [Min(0f)]
    [Tooltip("Critical Chance above this value is converted into ATK.")]
    public float critCap = 100f;

    [Min(0.01f)]
    [Tooltip("Critical Chance points converted into 1% ATK bonus (percent of base ATK).")]
    public float excessCritPerAtkPercent = 2f;

    [FormerlySerializedAs("blockOtherCritSources")]
    [Tooltip(
        "While evolved, every other Critical Chance bonus becomes ATK at the rate above. "
            + "Base CRT and kill stacks stay as Critical Chance."
    )]
    public bool convertOtherCritBonusesToAtk = true;
}
