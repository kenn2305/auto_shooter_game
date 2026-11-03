using UnityEngine;

[CreateAssetMenu(fileName = "DamageOverTimeStatus", menuName = "Weapon/Status/Damage Over Time")]
public sealed class DamageOverTimeStatusDefinition : StatusEffectDefinition
{
    [Header("Damage Per Tick")]
    [SerializeField, Min(0f)] private float flatDamage = 1f;

    [SerializeField, Min(0f)]
    [Tooltip("Fraction of the hit damage captured when the status was applied.")]
    private float sourceDamageMultiplier;
    [SerializeField, Min(0.01f)] private float tickInterval = 1f;
    [SerializeField] private bool tickImmediately;
    [SerializeField] private DamageElement element = DamageElement.Poison;

    public override float TickInterval => Mathf.Max(0.01f, tickInterval);
    public override bool TickImmediately => tickImmediately;

    public override void OnTick(StatusEffectController controller, ActiveStatusEffect runtime)
    {
        WeaponEffectContext source = runtime.SourceContext;
        IDamageReceiver target = controller.DamageReceiver;
        if (target == null || !target.IsAlive)
        {
            return;
        }

        float damagePerStack = flatDamage + source.SourceDamage.Amount * sourceDamageMultiplier;
        int amount = Mathf.Max(0, Mathf.RoundToInt(damagePerStack * runtime.Stacks));
        if (amount <= 0)
        {
            return;
        }

        DamagePacket damage = new DamagePacket(
            amount,
            source.Owner,
            DamageType.StatusEffect,
            controller.transform.position,
            isCritical: false,
            element
        );

        if (source.Controller != null)
        {
            source.Controller.ApplyEffectDamage(
                source,
                target,
                controller.gameObject,
                damage,
                WeaponEffectOrigin.StatusTick,
                canTriggerOnHit: false
            );
        }
        else
        {
            target.ReceiveDamage(damage);
        }
    }
}
