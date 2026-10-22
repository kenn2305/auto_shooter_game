using UnityEngine;

public readonly struct DamageCalculationContext
{
    public GameObject Source { get; }
    public DamageType Type { get; }
    public Vector2 HitPosition { get; }

    public float BaseDamage { get; }
    public float PowerPercent { get; }
    public float DamageMultiplier { get; }

    public float CriticalChancePercent { get; }
    public float CriticalMultiplier { get; }
    public float VariancePercent { get; }

    public DamageCalculationContext(
        GameObject source,
        DamageType type,
        Vector2 hitPosition,
        float baseDamage,
        float powerPercent,
        float damageMultiplier,
        float criticalChance,
        float criticalMulti,
        float variancePercent)
    {
        Source = source;
        Type = type;
        HitPosition = hitPosition;
        BaseDamage = baseDamage;
        PowerPercent = powerPercent;
        DamageMultiplier = damageMultiplier;
        CriticalChancePercent = criticalChance;
        CriticalMultiplier = criticalMulti;
        VariancePercent = variancePercent;
    }
}
