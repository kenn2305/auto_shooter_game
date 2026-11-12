using UnityEngine;

public static class WeaponAreaUtility
{
    public static float ResolveMultiplier(WeaponData weapon, CharacterCombatSystem combatStats)
    {
        float weaponMultiplier = weapon != null ? Mathf.Max(0f, weapon.areaMultiplier) : 1f;
        float playerMultiplier = combatStats != null
            ? Mathf.Max(0f, combatStats.GetMultiplier(CombatStatType.WeaponAreaPercent)) : 1f;
        return weaponMultiplier * playerMultiplier;
    }

    public static float ScaleNewInstance(Transform instance, WeaponData weapon, CharacterCombatSystem combatStats)
    {
        float multiplier = ResolveMultiplier(weapon, combatStats);
        if (instance != null)
        {
            instance.localScale *= multiplier;
        }

        return multiplier;
    }
}
