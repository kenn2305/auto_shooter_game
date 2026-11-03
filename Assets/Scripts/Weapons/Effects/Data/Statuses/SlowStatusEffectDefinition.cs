using UnityEngine;

[CreateAssetMenu(fileName = "SlowStatus", menuName = "Weapon/Status/Slow")]
public sealed class SlowStatusEffectDefinition : StatusEffectDefinition
{
    [SerializeField, Range(0f, 1f)] private float movementSpeedMultiplier = 0.7f;

    public override float MovementSpeedMultiplier => Mathf.Clamp(movementSpeedMultiplier, 0f, 1f);
}
