using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class CharacterCombatSystem : MonoBehaviour
{
    private const int StatCount = 6;
    private static readonly object BaseStatsOwner = new();
    private static readonly object InspectorBonusOwner = new();

    [Header("Base Stats")]
    [SerializeField]
    private CharacterStats baseStats;

    [Header("Inspector Bonus")]
    [SerializeField]
    [FormerlySerializedAs("attackBonus")]
    private float inspectorAttackBonus;

    [SerializeField]
    [FormerlySerializedAs("criticalHitBonus")]
    private float inspectorCriticalHitBonus;

    [SerializeField]
    [FormerlySerializedAs("hasteBonus")]
    private float inspectorAttackSpeedBonus;

    [SerializeField]
    private float inspectorMovementSpeedBonus;

    [SerializeField]
    private float inspectorWeaponAreaBonus;

    [SerializeField]
    private float inspectorExperiencePickupRangeBonus;

    private readonly Dictionary<object, RuntimeStatContribution> runtimeContributions = new();
    private readonly Dictionary<object, RuntimeRestrictionProvider> runtimeRestrictions = new();
    private readonly float[] cachedStats = new float[StatCount];
    private bool statsDirty = true;

    public event Action StatsChanged;

    public float AttackPercent => GetStat(CombatStatType.AttackPowerPercent);
    public float CriticalHitPercent => GetStat(CombatStatType.CriticalChancePercent);
    public float HastePercent => GetStat(CombatStatType.AttackSpeedPercent);
    public float MovementSpeedPercent => GetStat(CombatStatType.MovementSpeedPercent);
    public float WeaponAreaPercent => GetStat(CombatStatType.WeaponAreaPercent);
    public float ExperiencePickupRangePercent =>
        GetStat(CombatStatType.ExperiencePickupRangePercent);

    public float GetStat(CombatStatType stat)
    {
        int statIndex = (int)stat;
        if (statIndex < 0 || statIndex >= StatCount)
        {
            return 0f;
        }

        RebuildStatsIfNeeded();
        return cachedStats[statIndex];
    }

    public float GetMultiplier(CombatStatType stat)
    {
        return GetStat(stat) / 100f;
    }

    public void SetStatBonuses(
        object providerKey,
        CombatStatSource source,
        IReadOnlyList<CombatStatBonus> bonuses
    )
    {
        SetStatBonuses(providerKey, providerKey, source, bonuses);
    }

    public void SetStatBonuses(
        object providerKey,
        object ownerKey,
        CombatStatSource source,
        IReadOnlyList<CombatStatBonus> bonuses
    )
    {
        if (providerKey == null || ownerKey == null)
        {
            Debug.LogError("Stat provider and owner keys must be non-null.", this);
            return;
        }

        if (source == CombatStatSource.None || bonuses == null || bonuses.Count == 0)
        {
            RemoveStatBonuses(providerKey);
            return;
        }

        runtimeContributions[providerKey] = new RuntimeStatContribution(
            ownerKey,
            source,
            CopyBonuses(bonuses)
        );
        MarkStatsDirty();
    }

    public void SetStatRestrictions(
        object providerKey,
        IReadOnlyList<CombatStatRestriction> restrictions
    )
    {
        SetStatRestrictions(providerKey, providerKey, restrictions);
    }

    public void SetStatRestrictions(
        object providerKey,
        object ownerKey,
        IReadOnlyList<CombatStatRestriction> restrictions
    )
    {
        if (providerKey == null || ownerKey == null)
        {
            Debug.LogError("Restriction provider and owner keys must be non-null.", this);
            return;
        }

        if (restrictions == null || restrictions.Count == 0)
        {
            RemoveStatRestrictions(providerKey);
            return;
        }

        runtimeRestrictions[providerKey] = new RuntimeRestrictionProvider(
            ownerKey,
            CopyRestrictions(restrictions)
        );
        MarkStatsDirty();
    }

    public void RemoveStatBonuses(object providerKey)
    {
        if (providerKey != null && runtimeContributions.Remove(providerKey))
        {
            MarkStatsDirty();
        }
    }

    public void RemoveStatRestrictions(object providerKey)
    {
        if (providerKey != null && runtimeRestrictions.Remove(providerKey))
        {
            MarkStatsDirty();
        }
    }

    public void RemoveStatProvider(object providerKey)
    {
        if (providerKey == null)
        {
            return;
        }

        bool changed = runtimeContributions.Remove(providerKey);
        changed |= runtimeRestrictions.Remove(providerKey);
        if (changed)
        {
            MarkStatsDirty();
        }
    }

    public void AddAttackBonus(float bonus)
    {
        inspectorAttackBonus += bonus;
        MarkStatsDirty();
    }

    public void AddCriticalHitBonus(float bonus)
    {
        inspectorCriticalHitBonus += bonus;
        MarkStatsDirty();
    }

    public void AddHasteBonus(float bonus)
    {
        inspectorAttackSpeedBonus += bonus;
        MarkStatsDirty();
    }

    public void AddMovementSpeedBonus(float bonus)
    {
        inspectorMovementSpeedBonus += bonus;
        MarkStatsDirty();
    }

    public void AddWeaponAreaBonus(float bonus)
    {
        inspectorWeaponAreaBonus += bonus;
        MarkStatsDirty();
    }

    public void AddExperiencePickupRangeBonus(float bonus)
    {
        inspectorExperiencePickupRangeBonus += bonus;
        MarkStatsDirty();
    }

    public void RemoveAttackBonus(float bonus) => AddAttackBonus(-bonus);
    public void RemoveCriticalHitBonus(float bonus) => AddCriticalHitBonus(-bonus);
    public void RemoveHasteBonus(float bonus) => AddHasteBonus(-bonus);
    public void RemoveMovementSpeedBonus(float bonus) => AddMovementSpeedBonus(-bonus);
    public void RemoveWeaponAreaBonus(float bonus) => AddWeaponAreaBonus(-bonus);
    public void RemoveExperiencePickupRangeBonus(float bonus) =>
        AddExperiencePickupRangeBonus(-bonus);

    private void OnValidate()
    {
        statsDirty = true;
    }

    private void RebuildStatsIfNeeded()
    {
        if (!statsDirty)
        {
            return;
        }

        Array.Clear(cachedStats, 0, cachedStats.Length);

        for (int i = 0; i < StatCount; i++)
        {
            CombatStatType stat = (CombatStatType)i;
            ApplyContribution(BaseStatsOwner, CombatStatSource.BaseStats, stat, GetBaseValue(stat));
            ApplyContribution(
                InspectorBonusOwner,
                CombatStatSource.InspectorBonus,
                stat,
                GetInspectorBonus(stat)
            );
        }

        foreach (RuntimeStatContribution contribution in runtimeContributions.Values)
        {
            IReadOnlyList<CombatStatBonus> bonuses = contribution.Bonuses;
            for (int i = 0; i < bonuses.Count; i++)
            {
                CombatStatBonus bonus = bonuses[i];
                ApplyContribution(
                    contribution.OwnerKey,
                    contribution.Source,
                    bonus.stat,
                    bonus.value
                );
            }
        }

        for (int i = 0; i < cachedStats.Length; i++)
        {
            cachedStats[i] = Mathf.Max(0f, cachedStats[i]);
        }

        statsDirty = false;
    }

    private void ApplyContribution(
        object contributionOwner,
        CombatStatSource source,
        CombatStatType stat,
        float value
    )
    {
        int statIndex = (int)stat;
        if (statIndex < 0 || statIndex >= StatCount || Mathf.Approximately(value, 0f))
        {
            return;
        }

        if (
            TryFindWinningRestriction(
                contributionOwner,
                source,
                stat,
                out CombatStatRestriction restriction
            )
        )
        {
            if (
                restriction.mode == StatRestrictionMode.Convert
                && (int)restriction.convertedStat >= 0
                && (int)restriction.convertedStat < StatCount
                && restriction.convertedStat != stat
            )
            {
                cachedStats[(int)restriction.convertedStat] +=
                    value * Mathf.Max(0f, restriction.conversionRatio);
            }

            return;
        }

        cachedStats[statIndex] += value;
    }

    private bool TryFindWinningRestriction(
        object contributionOwner,
        CombatStatSource source,
        CombatStatType stat,
        out CombatStatRestriction winningRule
    )
    {
        winningRule = null;
        bool found = false;

        foreach (RuntimeRestrictionProvider provider in runtimeRestrictions.Values)
        {
            IReadOnlyList<CombatStatRestriction> rules = provider.Restrictions;
            for (int i = 0; i < rules.Count; i++)
            {
                CombatStatRestriction rule = rules[i];
                if (
                    rule == null
                    ||
                    rule.stat != stat
                    || (rule.affectedSources & source) == 0
                    || (rule.allowOwningSource && Equals(contributionOwner, provider.OwnerKey))
                )
                {
                    continue;
                }

                bool winsTie = found
                    && rule.priority == winningRule.priority
                    && rule.mode == StatRestrictionMode.Block
                    && winningRule.mode == StatRestrictionMode.Convert;
                if (!found || rule.priority > winningRule.priority || winsTie)
                {
                    winningRule = rule;
                    found = true;
                }
            }
        }

        return found;
    }

    private float GetBaseValue(CombatStatType stat)
    {
        return stat switch
        {
            CombatStatType.AttackPowerPercent => baseStats != null ? baseStats.ATK : 100f,
            CombatStatType.CriticalChancePercent => baseStats != null ? baseStats.CRT : 0f,
            CombatStatType.AttackSpeedPercent => baseStats != null ? baseStats.Haste : 0f,
            CombatStatType.MovementSpeedPercent => baseStats != null ? baseStats.SPD : 100f,
            CombatStatType.WeaponAreaPercent => 100f,
            CombatStatType.ExperiencePickupRangePercent =>
                baseStats != null ? baseStats.PUR : 100f,
            _ => 0f,
        };
    }

    private float GetInspectorBonus(CombatStatType stat)
    {
        return stat switch
        {
            CombatStatType.AttackPowerPercent => inspectorAttackBonus,
            CombatStatType.CriticalChancePercent => inspectorCriticalHitBonus,
            CombatStatType.AttackSpeedPercent => inspectorAttackSpeedBonus,
            CombatStatType.MovementSpeedPercent => inspectorMovementSpeedBonus,
            CombatStatType.WeaponAreaPercent => inspectorWeaponAreaBonus,
            CombatStatType.ExperiencePickupRangePercent =>
                inspectorExperiencePickupRangeBonus,
            _ => 0f,
        };
    }

    private void MarkStatsDirty()
    {
        statsDirty = true;
        StatsChanged?.Invoke();
    }

    private static CombatStatBonus[] CopyBonuses(IReadOnlyList<CombatStatBonus> bonuses)
    {
        CombatStatBonus[] copy = new CombatStatBonus[bonuses.Count];
        for (int i = 0; i < bonuses.Count; i++)
        {
            copy[i] = bonuses[i];
        }

        return copy;
    }

    private static CombatStatRestriction[] CopyRestrictions(
        IReadOnlyList<CombatStatRestriction> restrictions
    )
    {
        CombatStatRestriction[] copy = new CombatStatRestriction[restrictions.Count];
        for (int i = 0; i < restrictions.Count; i++)
        {
            copy[i] = restrictions[i] != null
                ? new CombatStatRestriction(restrictions[i])
                : null;
        }

        return copy;
    }

    private readonly struct RuntimeStatContribution
    {
        public object OwnerKey { get; }
        public CombatStatSource Source { get; }
        public IReadOnlyList<CombatStatBonus> Bonuses { get; }

        public RuntimeStatContribution(
            object ownerKey,
            CombatStatSource source,
            IReadOnlyList<CombatStatBonus> bonuses
        )
        {
            OwnerKey = ownerKey;
            Source = source;
            Bonuses = bonuses;
        }
    }

    private readonly struct RuntimeRestrictionProvider
    {
        public object OwnerKey { get; }
        public IReadOnlyList<CombatStatRestriction> Restrictions { get; }

        public RuntimeRestrictionProvider(
            object ownerKey,
            IReadOnlyList<CombatStatRestriction> restrictions
        )
        {
            OwnerKey = ownerKey;
            Restrictions = restrictions;
        }
    }
}
