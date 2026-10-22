using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MeleeAwakenData", menuName = "Scriptable Objects/MeleeAwakenData")]
public sealed class MeleeAwakenData : ScriptableObject
{
    [Min(0f)]
    public int requiredLevelIndex;

    public WeaponTriggeredEffectData effect;
}
