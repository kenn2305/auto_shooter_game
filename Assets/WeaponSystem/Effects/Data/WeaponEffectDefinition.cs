using UnityEngine;

public abstract class WeaponEffectDefinition : ScriptableObject
{
    public abstract void Apply(in WeaponEffectContext context);
}
