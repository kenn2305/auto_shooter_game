using UnityEngine;
using UnityEngine.Serialization;

public enum WeaponAttackActorLifetime
{
    Transient,
    Persistent,
}

[CreateAssetMenu(
    fileName = "WeaponAttackDefinition",
    menuName = "Weapon/Attack Definition"
)]
public class WeaponAttackDefinition : ScriptableObject
{
    [Header("Identity")]
    public string attackID;
    public string displayName;

    [Header("Execution")]
    [FormerlySerializedAs("lifeTime")]
    public WeaponAttackActorLifetime lifetime = WeaponAttackActorLifetime.Transient;

    [Header("Attack Prefab")]
    public WeaponAttackActorBase attackPrefab;
    public string animatorStateName;

    public WeaponHitRule hitRule = WeaponHitRule.OnePerAttack;

    [Tooltip("Number enemy can be hit each attack, -1 = unlimited")]
    [Min(-1f)] public int hitLimit = -1;
    [Min(0.01f)] public float hitCooldown = 1f;
}
