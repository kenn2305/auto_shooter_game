using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Weapon/Weapon Data")]
public sealed class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string weaponID;
    public string displayName;
    public Sprite icon;

    [Header("Base Damage")]
    [Min(0f)] public float baseDamage;
    [Min(1f)] public float criticalHitMultiplier = 1.75f;
    [Range(0f, 100f)] public float damageVariancePercent = 10f;

    [Header("Rules")]
    [FormerlySerializedAs("affectByHaste")]
    public bool affectedByHaste = true;

    public LayerMask targetLayerMask;

    [Header("Levels")]
    public List<WeaponLevelData> levels = new();

    public WeaponLevelData GetLevel(int index)
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError($"WeaponData {weaponID} has no levels defined.");
            return null;
        }

        return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
    }
}
