using UnityEngine;

[CreateAssetMenu(fileName = "DirectDamageEffect", menuName = "Weapon/Effect/Direct Damage")]
public sealed class DirectDamageWeaponEffectDefinition : WeaponEffectDefinition
{
    [SerializeField, Min(0f)] private float flatDamage;
    [SerializeField, Min(0f)] private float sourceDamageMultiplier = 1f;
    [SerializeField] private DamageElement element = DamageElement.Physical;
    [SerializeField] private bool canTriggerOnHit;

    public override void Apply(in WeaponEffectContext context)
    {
        if (context.Target == null || !context.Target.IsAlive)
        {
            return;
        }

        int amount = Mathf.Max(0, Mathf.RoundToInt(flatDamage + context.SourceDamage.Amount * sourceDamageMultiplier));
        if (amount <= 0)
        {
            return;
        }

        DamagePacket damage = new DamagePacket(
            amount,
            context.Owner,
            DamageType.Weapon,
            context.Position,
            isCritical: false,
            element
        );

        if (context.Controller != null)
        {
            context.Controller.ApplyEffectDamage(
                context,
                context.Target,
                context.TargetObject,
                damage,
                WeaponEffectOrigin.DirectEffect,
                canTriggerOnHit
            );
        }
        else
        {
            context.Target.ReceiveDamage(damage);
        }
    }
}
