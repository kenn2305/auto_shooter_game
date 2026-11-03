using System;
using UnityEngine;

[CreateAssetMenu(fileName = ResourceName, menuName = "Experience/Experience System Config")]
public sealed class ExperienceSystemConfig : ScriptableObject
{
    public const string ResourceName = "ExperienceSystemConfig";

    [Header("HoloCure Level Curve")]
    [Min(1)]
    public int firstLevelRequirement = 79;

    [Min(0.01f)]
    public float curveScale = 4f;

    [Min(1f)]
    public float curveExponent = 2.1f;

    [Header("Normal Enemy Drop Ratios (One Drop Guaranteed)")]
    [Tooltip("Relative chance for Normal EXP. All three ratios are automatically normalized to 100%.")]
    [Range(0f, 100f)]
    [InspectorName("Normal EXP Ratio")]
    public float normalWeight = 80f;

    [Tooltip("Relative chance for Green EXP. All three ratios are automatically normalized to 100%.")]
    [Range(0f, 100f)]
    [InspectorName("Green EXP Ratio")]
    public float greenWeight = 15f;

    [Tooltip("Relative chance for Orange EXP. All three ratios are automatically normalized to 100%.")]
    [Range(0f, 100f)]
    [InspectorName("Orange EXP Ratio")]
    public float orangeWeight = 5f;

    [Header("EXP Multipliers")]
    [Min(0f)]
    public float normalMultiplier = 1f;

    [Min(0f)]
    public float greenMultiplier = 1.25f;

    [Min(0f)]
    public float orangeMultiplier = 1.5f;

    [Min(0f)]
    public float redMultiplier = 2f;

    [Header("Rainbow Magnet")]
    [Tooltip("Independent percentage chance per enemy reward. HoloCure default 1/3000 = 0.033333%.")]
    [Range(0f, 100f)]
    public float rainbowDropChancePercent = 100f / 3000f;

    [Header("Pickup")]
    [Min(0.01f)]
    public float basePickupRadius = 2.5f;

    [Min(0.01f)]
    public float collectionDistance = 0.2f;

    [Min(0f)]
    public float initialAttractionSpeed = 8f;

    [Min(0f)]
    public float attractionAcceleration = 30f;

    [Min(0f)]
    public float maximumAttractionSpeed = 35f;

    [Header("Visual")]
    [Tooltip("Optional authored triangle sprite. A runtime triangle is generated when empty.")]
    public Sprite pickupSprite;

    public Color normalColor = new(0.25f, 0.65f, 1f, 1f);
    public Color greenColor = new(0.25f, 1f, 0.35f, 1f);
    public Color orangeColor = new(1f, 0.48f, 0.08f, 1f);
    public Color redColor = new(1f, 0.12f, 0.16f, 1f);

    [Min(0.01f)]
    public float regularWorldSize = 0.42f;

    [Min(0.01f)]
    public float rainbowWorldSize = 0.72f;

    [Min(0f)]
    public float rainbowPulseAmount = 0.12f;

    [Min(0f)]
    public float rainbowPulseSpeed = 5f;

    [Min(0f)]
    public float rainbowHueSpeed = 0.35f;

    [Header("Pool")]
    [Min(1)]
    public int initialPoolSize = 64;

    [Min(1)]
    public int maximumPoolSize = 4096;

    public int GetExperienceRequired(int currentLevel)
    {
        currentLevel = Mathf.Max(1, currentLevel);
        if (currentLevel == 1)
        {
            return Mathf.Max(1, firstLevelRequirement);
        }

        double currentTotal = Math.Round(
            Math.Pow(curveScale * currentLevel, curveExponent),
            MidpointRounding.ToEven
        );
        double nextTotal = Math.Round(
            Math.Pow(curveScale * (currentLevel + 1), curveExponent),
            MidpointRounding.ToEven
        );
        if (
            double.IsNaN(currentTotal)
            || double.IsInfinity(currentTotal)
            || double.IsNaN(nextTotal)
            || double.IsInfinity(nextTotal)
        )
        {
            return int.MaxValue;
        }

        double required = Math.Max(1d, nextTotal - currentTotal);
        return required >= int.MaxValue ? int.MaxValue : (int)required;
    }

    public float NormalEnemyConfiguredChanceTotal => normalWeight + greenWeight + orangeWeight;

    public ExperiencePickupType RollNormalEnemyDrop(float randomValue)
    {
        float normalRatio = Mathf.Max(0f, normalWeight);
        float greenRatio = Mathf.Max(0f, greenWeight);
        float orangeRatio = Mathf.Max(0f, orangeWeight);
        float totalRatio = normalRatio + greenRatio + orangeRatio;
        if (totalRatio <= 0f)
        {
            return ExperiencePickupType.Normal;
        }

        float roll = Mathf.Clamp(randomValue, 0f, 0.999999f) * totalRatio;

        if (roll < normalRatio)
        {
            return ExperiencePickupType.Normal;
        }

        if (roll < normalRatio + greenRatio)
        {
            return ExperiencePickupType.Green;
        }

        return ExperiencePickupType.Orange;
    }

    public bool RollPercentChance(float chancePercent, float randomValue)
    {
        float clampedChance = Mathf.Clamp(chancePercent, 0f, 100f);
        if (clampedChance <= 0f)
        {
            return false;
        }

        if (clampedChance >= 100f)
        {
            return true;
        }

        return Mathf.Clamp01(randomValue) * 100f < clampedChance;
    }

    public float GetEffectiveNormalEnemyChancePercent(ExperiencePickupType type)
    {
        float normalRatio = Mathf.Max(0f, normalWeight);
        float greenRatio = Mathf.Max(0f, greenWeight);
        float orangeRatio = Mathf.Max(0f, orangeWeight);
        float totalRatio = normalRatio + greenRatio + orangeRatio;
        if (totalRatio <= 0f)
        {
            return type == ExperiencePickupType.Normal ? 100f : 0f;
        }

        return type switch
        {
            ExperiencePickupType.Normal => normalRatio / totalRatio * 100f,
            ExperiencePickupType.Green => greenRatio / totalRatio * 100f,
            ExperiencePickupType.Orange => orangeRatio / totalRatio * 100f,
            _ => 0f,
        };
    }

    public int CalculateDropValue(int baseExperience, ExperiencePickupType type)
    {
        float multiplier = type switch
        {
            ExperiencePickupType.Green => greenMultiplier,
            ExperiencePickupType.Orange => orangeMultiplier,
            ExperiencePickupType.Red => redMultiplier,
            _ => normalMultiplier,
        };
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseExperience) * multiplier));
    }

    public Color GetColor(ExperiencePickupType type)
    {
        return type switch
        {
            ExperiencePickupType.Green => greenColor,
            ExperiencePickupType.Orange => orangeColor,
            ExperiencePickupType.Red => redColor,
            _ => normalColor,
        };
    }
}
