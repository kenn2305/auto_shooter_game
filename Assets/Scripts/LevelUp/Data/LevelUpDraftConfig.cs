using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = ResourceName, menuName = "Level Up/Draft Config")]
public sealed class LevelUpDraftConfig : ScriptableObject
{
    public const string ResourceName = "LevelUpDraftConfig";

    [Header("Draft Rules")]
    [Min(3)] public int choiceCount = 3;
    [Min(0)] public int maximumWeaponSlots = 2;
    [Min(0)] public int maximumItemSlots = 2;

    [Header("Available Equipment")]
    public List<LevelUpWeaponOfferDefinition> weapons = new();
    public List<LevelUpItemOfferDefinition> items = new();

    [Header("Stat Packages")]
    public List<LevelUpStatDefinition> statPackages = new();

    [Header("Level-based Balance")]
    [Tooltip("The phase with the highest Min Player Level not exceeding the current level is used.")]
    public List<LevelUpDraftPhase> phases = new();

    public LevelUpDraftPhase GetPhase(int playerLevel)
    {
        LevelUpDraftPhase selected = null;
        int selectedMinimum = int.MinValue;
        if (phases != null)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                LevelUpDraftPhase phase = phases[i];
                if (phase != null && phase.minPlayerLevel <= playerLevel && phase.minPlayerLevel >= selectedMinimum)
                {
                    selected = phase;
                    selectedMinimum = phase.minPlayerLevel;
                }
            }
        }

        return selected ?? LevelUpDraftPhase.CreateFallback();
    }
}
