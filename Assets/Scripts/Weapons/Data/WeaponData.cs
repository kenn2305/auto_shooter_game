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

    [Header("Attack Speed")]
    [Tooltip("Time in seconds between attacks. Shared by every level of this weapon.")]
    [Min(0.01f)] public float attackInterval = 1f;

    [Tooltip("Minimum time between attacks. Set to -1 for no limit.")]
    [Min(-1f)] public float minimumAttackInterval = 0.05f;

    [Header("Area")]
    [Tooltip("Value of hitbox size multiplier, 1 = default size. Shared by every level of this weapon.")]
    [Min(0f)] public float areaMultiplier = 1f;

    [Header("Normal Levels")]
    public List<WeaponLevelData> levels = new();

    [Tooltip("The max level: the player picks one of these forms after the last normal level.")]
    public List<WeaponEvolutionData> evolutions = new();

    [Header("Evolution Passive")]
    public WeaponEvolutionCritStackData evolutionCritStack = new();

    public WeaponLevelData LastNormalLevel => levels != null && levels.Count > 0 ? levels[levels.Count - 1] : null;

    public bool IsFormConfigured(WeaponEvolutionData form)
    {
        return form != null && form.IsConfiguredFor(LastNormalLevel);
    }

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
