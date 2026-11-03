using UnityEngine;

[CreateAssetMenu(fileName = "ApplyStatusEffect", menuName = "Weapon/Effect/Apply Status")]
public sealed class ApplyStatusWeaponEffectDefinition : WeaponEffectDefinition
{
    [SerializeField]
    private StatusEffectDefinition status;

    public override void Apply(in WeaponEffectContext context)
    {
        if (status == null || context.TargetObject == null)
        {
            return;
        }

        StatusEffectController receiver =
            context.TargetObject.GetComponentInParent<StatusEffectController>();
        receiver?.ApplyStatus(status, context);
    }
}
