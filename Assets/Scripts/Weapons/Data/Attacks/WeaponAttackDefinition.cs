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

    [Header("Repeat")]
    [Tooltip("How many times one attack is performed in a row. 2 means every attack strikes twice.")]
    [Min(1)] public int attackRepeatCount = 1;

    [Tooltip("Base seconds between the repeated strikes. Haste shortens this delay.")]
    [Min(0f)] public float attackRepeatDelay = 0.2f;

    [Header("Damage")]
    [Tooltip("Multiplies every hit of this attack. 1.25 means 25% more damage.")]
    [Min(0f)] public float damageMultiplier = 1f;

    [Header("Status Damage Bonus")]
    [Tooltip("Targets that currently have this status take extra damage from this attack. Empty means no bonus.")]
    public StatusEffectDefinition bonusDamageStatus;

    [Tooltip("Percentage points of extra damage. 20 means +20%.")]
    [Min(0f)] public float bonusDamagePercent;

    public float GetDamageMultiplierAgainst(IDamageReceiver target)
    {
        if (bonusDamageStatus == null || bonusDamagePercent <= 0f || target is not Component component)
        {
            return damageMultiplier;
        }

        StatusEffectController statuses = component.GetComponent<StatusEffectController>();
        bool hasBonus = statuses != null && statuses.HasStatus(bonusDamageStatus);
        return hasBonus ? damageMultiplier * (1f + bonusDamagePercent / 100f) : damageMultiplier;
    }
}
