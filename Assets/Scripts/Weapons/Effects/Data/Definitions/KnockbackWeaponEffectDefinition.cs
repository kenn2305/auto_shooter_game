using UnityEngine;

[CreateAssetMenu(fileName = "KnockbackEffect", menuName = "Weapon/Effect/Knockback")]
public sealed class KnockbackWeaponEffectDefinition : WeaponEffectDefinition
{
    [SerializeField, Min(0f)] private float strength = 3f;

    public override void Apply(in WeaponEffectContext context)
    {
        if (
            context.TargetObject == null
            || !WeaponTargetResolver.TryResolveInterface(context.TargetObject, out IKnockbackReceiver receiver)
        )
        {
            return;
        }

        Vector2 direction = context.Direction;
        if (context.Owner != null)
        {
            direction = (Vector2)context.TargetObject.transform.position - (Vector2)context.Owner.transform.position;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.up;
        }

        receiver.ApplyKnockback(direction.normalized * strength);
    }
}
