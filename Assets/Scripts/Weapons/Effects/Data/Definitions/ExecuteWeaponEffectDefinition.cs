using UnityEngine;

/// <summary>
/// Kills the target outright. Set the chance with Trigger Chance on the Triggered Effect entry.
/// </summary>
[CreateAssetMenu(fileName = "ExecuteEffect", menuName = "Weapon/Effect/Execute")]
public sealed class ExecuteWeaponEffectDefinition : WeaponEffectDefinition
{
    [SerializeField, Range(0f, 100f)]
    [Tooltip("Elite and Boss enemies lose this percent of their maximum health instead of dying. 100 kills them too.")]
    private float eliteBossMaxHealthPercent = 100f;

    public override void Apply(in WeaponEffectContext context)
    {
        if (context.Target == null || !context.Target.IsAlive)
        {
            return;
        }

        int amount = ResolveAmount(context);
        if (amount <= 0)
        {
            return;
        }

        DamagePacket damage = new DamagePacket(
            amount,
            context.Owner,
            DamageType.Weapon,
            context.Position,
            isCritical: false
        );

        if (context.Controller != null)
        {
            context.Controller.ApplyEffectDamage(
                context,
                context.Target,
                context.TargetObject,
                damage,
                WeaponEffectOrigin.DirectEffect,
                canTriggerOnHit: false
            );
        }
        else
        {
            context.Target.ReceiveDamage(damage);
        }
    }

    private int ResolveAmount(in WeaponEffectContext context)
    {
        EnemyExperienceDropper rank = context.TargetObject != null
            ? context.TargetObject.GetComponentInParent<EnemyExperienceDropper>()
            : null;

        if (eliteBossMaxHealthPercent >= 100f || rank == null || rank.Rank == EnemyExperienceRank.Normal)
        {
            return int.MaxValue;
        }

        return context.Target is EnemyHealth health
            ? Mathf.CeilToInt(health.MaxHealth * eliteBossMaxHealthPercent / 100f)
            : 0;
    }
}
