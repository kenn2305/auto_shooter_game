using UnityEngine;

public static class WeaponDamageCalculator
{
    private static readonly IDamageCalculator Calculator = new DefaultDamageCalculator();

    public static DamagePacket Calculate(
        GameObject owner,
        WeaponData weapon,
        WeaponLevelData level,
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
            powerPercent: stats != null ? stats.GetStat(CombatStatType.AttackPowerPercent) : 100f,
            damageMultiplier: level.damage * Mathf.Max(0f, additionalMultiplier),
            criticalChance: stats != null ? stats.GetStat(CombatStatType.CriticalChancePercent) : 0f,
            criticalMulti: weapon.criticalHitMultiplier,
            variancePercent: weapon.damageVariancePercent
        );

        return Calculator.Calculate(context);
    }
}
