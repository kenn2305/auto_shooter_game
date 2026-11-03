using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class WeaponTriggeredEffectData
{
    [Header("Trigger")]
    public WeaponEffectTrigger trigger;

    [Range(0f, 1f)]
    public float triggerChance = 1f;

    [Min(0f)]
    [Tooltip("Minimum time before this entry can trigger again on this weapon instance.")]
    public float internalCooldown;

    [Tooltip("Allow this entry to trigger from projectile, status, mark, or other secondary hits.")]
    public bool allowSecondaryHits;

    [Header("Effect")]
    [Tooltip("Data-driven effect. Preferred for new effects.")]
    public WeaponEffectDefinition effect;

    [Tooltip(
        "Actor prefab used by effects that need their own runtime GameObject, "
            + "such as the Slash projectile or an explosion. Assign either Effect or Effect Prefab."
    )]
    public GameObject effectPrefab;

    [Tooltip("Allow hits created by this effect to proc OnHit effects.")]
    public bool generatedHitsCanTriggerOnHit;

    [Min(0f)]
    [FormerlySerializedAs("damageMutiplier")]
    public float damageMultiplier = 1f;
}
