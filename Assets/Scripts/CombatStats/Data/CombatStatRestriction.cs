using System;
using UnityEngine;

[Serializable]
public sealed class CombatStatRestriction
{
    [Tooltip("Stat affected by this global player rule.")]
    public CombatStatType stat;

    [Tooltip("Categories rejected or converted by this rule.")]
    public CombatStatSource affectedSources = CombatStatSource.All;

    [Tooltip("Block the incoming value, or convert it into another stat.")]
    public StatRestrictionMode mode;

    [Tooltip("Used only in Convert mode.")]
    public CombatStatType convertedStat;

    [Tooltip("Incoming value x this ratio is added as a bonus on the converted stat, using that stat's bonus rule.")]
    [Min(0f)] public float conversionRatio = 1f;

    [Tooltip("Higher priority wins when multiple active rules target the same stat.")]
    public int priority;

    [Tooltip(
        "The equipment that owns this rule may still grant the restricted stat. "
            + "Other providers owned by that equipment can share its owner key."
    )]
    public bool allowOwningSource = true;

    public CombatStatRestriction() { }

    public CombatStatRestriction(CombatStatRestriction other)
    {
        stat = other.stat;
        affectedSources = other.affectedSources;
        mode = other.mode;
        convertedStat = other.convertedStat;
        conversionRatio = other.conversionRatio;
        priority = other.priority;
        allowOwningSource = other.allowOwningSource;
    }

    public static CombatStatRestriction Block(
        CombatStatType stat,
        CombatStatSource affectedSources = CombatStatSource.All,
        int priority = 0
    )
    {
        return new CombatStatRestriction
        {
            stat = stat,
            affectedSources = affectedSources,
            mode = StatRestrictionMode.Block,
            convertedStat = stat,
            conversionRatio = 0f,
            priority = priority,
            allowOwningSource = true,
        };
    }
}
