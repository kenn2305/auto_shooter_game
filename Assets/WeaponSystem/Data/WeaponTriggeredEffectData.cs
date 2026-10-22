using System;
using UnityEngine;

[Serializable]
public class WeaponTriggeredEffectData
{
    [Header("Trigger")]
    public WeaponEffectTrigger trigger;

    [Range(0f, 1f)]
    public float triggerChance = 1f;

    [Header("Effect")]
    public WeaponTriggeredEffectActor effectPrefab;

    [Min(0f)]
    public float damageMutiplier = 1f;
}
