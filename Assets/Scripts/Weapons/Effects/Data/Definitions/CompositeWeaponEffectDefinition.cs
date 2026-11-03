using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CompositeEffect", menuName = "Weapon/Effect/Composite")]
public sealed class CompositeWeaponEffectDefinition : WeaponEffectDefinition
{
    [SerializeField] private List<WeaponEffectDefinition> effects = new();

    public override void Apply(in WeaponEffectContext context)
    {
        if (effects == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            effects[i]?.Apply(context);
        }
    }
}
