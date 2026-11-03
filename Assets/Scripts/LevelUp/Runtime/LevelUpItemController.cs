using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelUpItemController : MonoBehaviour
{
    [SerializeField] private LevelUpItemData itemData;
    [SerializeField] private CharacterCombatSystem combatStats;
    [SerializeField, Min(0)] private int currentLevelIndex;

    private readonly List<CombatStatBonus> cumulativeBonuses = new();
    private readonly List<CombatStatRestriction> cumulativeRestrictions = new();

    public LevelUpItemData Data => itemData;
    public int CurrentLevelIndex => currentLevelIndex;
    public int DisplayLevel => currentLevelIndex + 1;
    public int MaximumLevel => itemData?.levels?.Count ?? 0;
    public bool IsMaxLevel => MaximumLevel <= 0 || currentLevelIndex >= MaximumLevel - 1;
    public LevelUpItemLevelData CurrentLevel => itemData?.GetLevel(currentLevelIndex);

    private void Awake()
    {
        ResolveReferences();
        ClampLevel();
    }

    private void OnEnable()
    {
        RefreshStatProvider();
    }

    private void OnDisable()
    {
        combatStats?.RemoveStatProvider(this);
    }

    public bool ConfigureRuntime(LevelUpItemData data, CharacterCombatSystem ownerCombatStats)
    {
        if (data == null || data.levels == null || data.levels.Count == 0)
        {
            Debug.LogError("Cannot configure an Item without at least one level.", this);
            return false;
        }

        itemData = data;
        combatStats = ownerCombatStats;
        currentLevelIndex = 0;
        ResolveReferences();
        RefreshStatProvider();
        return true;
    }

    public bool LevelUp()
    {
        if (IsMaxLevel)
        {
            return false;
        }

        currentLevelIndex++;
        RefreshStatProvider();
        return true;
    }

    private void ResolveReferences()
    {
        if (combatStats == null)
        {
            combatStats = GetComponentInParent<CharacterCombatSystem>(true);
        }
    }

    private void ClampLevel()
    {
        if (itemData?.levels == null || itemData.levels.Count == 0)
        {
            currentLevelIndex = 0;
            return;
        }

        currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, itemData.levels.Count - 1);
    }

    private void RefreshStatProvider()
    {
        if (!isActiveAndEnabled || combatStats == null || itemData?.levels == null || itemData.levels.Count == 0)
        {
            combatStats?.RemoveStatProvider(this);
            return;
        }

        cumulativeBonuses.Clear();
        cumulativeRestrictions.Clear();
        int lastLevel = Mathf.Clamp(currentLevelIndex, 0, itemData.levels.Count - 1);
        for (int i = 0; i <= lastLevel; i++)
        {
            LevelUpItemLevelData level = itemData.levels[i];
            if (level?.statBonuses != null)
            {
                cumulativeBonuses.AddRange(level.statBonuses);
            }

            if (level?.statRestrictions != null)
            {
                cumulativeRestrictions.AddRange(level.statRestrictions);
            }
        }

        combatStats.SetStatBonuses(this, CombatStatSource.Equipment, cumulativeBonuses);
        combatStats.SetStatRestrictions(this, cumulativeRestrictions);
    }
}
