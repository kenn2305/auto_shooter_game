using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MeleeWeaponData", menuName = "Scriptable Objects/MeleeWeaponData")]
public class MeleeWeaponData : ScriptableObject
{
    [Header("Identity")]
    public string weaponID;
    public string displayName;
    public Sprite icon;

    [Header("Base Damage")]
    [Min(0f)]
    public float baseDamage;

    [Min(1f)]
    public float criticalHitMultiplier = 1.75f;

    [Range(0f, 100f)]
    public float damageVariancePercent = 10f;

    [Header("Rules")]
    public bool affectByHaste = true;
    public LayerMask targetLayerMask;

    [Header("Levels")]
    public List<MeleeWeaponLevelData> levels = new List<MeleeWeaponLevelData>();

    [Header("Awakening")]
    public MeleeAwakenData awkening;

    public MeleeWeaponLevelData GetLevel(int index)
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError($"MeleeWeaponData {weaponID} has no levels defined.");
            return null;
        }

        return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
    }
}
