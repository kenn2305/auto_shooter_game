using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelUpItemData", menuName = "Level Up/Item Data")]
public sealed class LevelUpItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemID;
    public string displayName;
    public Sprite icon;

    [Header("Levels")]
    public List<LevelUpItemLevelData> levels = new();

    public LevelUpItemLevelData GetLevel(int index)
    {
        if (levels == null || levels.Count == 0)
        {
            return null;
        }

        return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
    }
}

[Serializable]
public sealed class LevelUpItemLevelData
{
    [TextArea]
    public string description;

    [Tooltip("Bonuses are cumulative with all earlier Item levels.")]
    public List<CombatStatBonus> statBonuses = new();

    [Tooltip("Global Stat rules unlocked at this Item level.")]
    public List<CombatStatRestriction> statRestrictions = new();
}
