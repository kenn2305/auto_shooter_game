using UnityEngine;

public sealed class DefaultDamageCalculator : IDamageCalculator
{
    private static float ApplyVariance(
        float damage,
        float variancePercent
    )
    {
        float variance = Mathf.Max(
            0f,
            variancePercent / 100f
        );

        if (variance <= 0f) return damage;

        return damage * Random.Range(1f - variance, 1f + variance);
    }

    public DamagePacket Calculate(in DamageCalculationContext context)
    {
        float damage = context.BaseDamage
                    * Mathf.Max(0f, context.PowerPercent / 100f)
                    * Mathf.Max(0f, context.DamageMultiplier);
        damage = ApplyVariance(
            damage,
            context.VariancePercent
        );

        bool isCritical = Random.value < Mathf.Clamp01(
            context.CriticalChancePercent / 100f
        );

        if (isCritical)
        {
            damage *= Mathf.Max(
                1f,
                context.CriticalMultiplier
            );
        }

        return new DamagePacket(
            amount: Mathf.RoundToInt(damage),
            source: context.Source,
            type: context.Type,
            hitPosition: context.HitPosition,
            isCritical: isCritical
        );
    }
}
