using UnityEngine;

/// <summary>
/// An attack that detonates explosions on the enemies nearest to the character that are visible to the camera.
/// Attack Prefab must hold a TargetedExplosionAttackActor.
/// </summary>
[CreateAssetMenu(fileName = "TargetedExplosionAttack", menuName = "Weapon/Attack/Targeted Explosion")]
public sealed class TargetedExplosionAttackDefinition : WeaponAttackDefinition
{
    [Header("Explosions")]
    [Tooltip("Visual only. Spawned on each target and destroyed after Visual Lifetime.")]
    public GameObject explosionPrefab;

    [Tooltip("How many nearest visible enemies are picked. Fewer visible enemies means fewer explosions.")]
    [Min(1)] public int explosionCount = 3;

    [Tooltip("Base seconds between two explosions of one attack. Haste shortens this delay.")]
    [Min(0f)] public float explosionInterval = 0.15f;

    [Tooltip("Damage radius in world units. Scales with Area.")]
    [Min(0.01f)] public float explosionRadius = 2f;

    [Min(0.01f)] public float visualLifetime = 0.4f;
}
