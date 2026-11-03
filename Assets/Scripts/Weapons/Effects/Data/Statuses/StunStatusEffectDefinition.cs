using UnityEngine;

[CreateAssetMenu(fileName = "StunStatus", menuName = "Weapon/Status/Stun")]
public sealed class StunStatusEffectDefinition : StatusEffectDefinition
{
    public override bool StunsMovement => true;
}
