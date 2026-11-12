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

    [SerializeField]
    [Tooltip("Each tick rolls the owner's Critical Chance and uses the weapon's Critical Hit Multiplier.")]
    private bool canCritical;

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
        float tickDamage = damagePerStack * runtime.Stacks;
        bool isCritical = RollCritical(source);
        if (isCritical)
        {
            tickDamage *= Mathf.Max(1f, source.Weapon.criticalHitMultiplier);
        }

        int amount = Mathf.Max(0, Mathf.RoundToInt(tickDamage));
        if (amount <= 0)
        {
            return;
        }

        DamagePacket damage = new DamagePacket(
            amount,
            source.Owner,
            DamageType.StatusEffect,
            controller.transform.position,
            isCritical,
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

    private bool RollCritical(in WeaponEffectContext source)
    {
        return canCritical
            && source.Stats != null
            && source.Weapon != null
            && Random.value < Mathf.Clamp01(source.Stats.GetStat(CombatStatType.CriticalChancePercent) / 100f);
    }
}
