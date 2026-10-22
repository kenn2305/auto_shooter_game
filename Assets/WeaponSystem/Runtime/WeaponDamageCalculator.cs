using UnityEngine;

public static class MeleeWeaponDamageCalculator
{
    private static readonly IDamageCalculator Calculator = new DefaultDamageCalculator();

    public static DamagePacket Calculate(
        GameObject owner,
        MeleeWeaponData weapon,
        MeleeWeaponLevelData level,
        CharacterCombatSystem stats,
        Vector2 hitPosition,
        DamageType damageType,
        float additionalMultiplier = 1f
    )
    {
        DamageCalculationContext context = new DamageCalculationContext(
            source: owner,
            type: damageType,
            hitPosition: hitPosition,
            baseDamage: weapon.baseDamage,

            powerPercent: stats != null ? stats.AttackPercent : 100f,
            damageMultiplier: level.damage * Mathf.Max(0f, additionalMultiplier),
            criticalChance: stats != null ? stats.CriticalHitPercent : 0f,
            criticalMulti: weapon.criticalHitMultiplier,
            variancePercent: weapon.damageVariancePercent
        );

        return Calculator.Calculate(context);
    }
}
