using UnityEngine;

public static class WeaponAreaUtility
{
    public static float ResolveMultiplier(WeaponLevelData level, CharacterCombatSystem combatStats)
    {
        float levelMultiplier = level != null ? Mathf.Max(0f, level.areaMultiplier) : 1f;
        float playerMultiplier = combatStats != null
            ? Mathf.Max(0f, combatStats.GetMultiplier(CombatStatType.WeaponAreaPercent)) : 1f;
        return levelMultiplier * playerMultiplier;
    }

    public static float ScaleNewInstance(Transform instance, WeaponLevelData level, CharacterCombatSystem combatStats)
    {
        float multiplier = ResolveMultiplier(level, combatStats);
        if (instance != null)
        {
            instance.localScale *= multiplier;
        }

        return multiplier;
    }
}
