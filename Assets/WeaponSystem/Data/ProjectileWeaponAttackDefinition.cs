using UnityEngine;

public enum ProjectileShotMode
{
    Shot,
    Multishot,
    Burst,
    BurstSequence,
}

public enum ProjectileSpawnPattern
{
    Forward,
    Cone,
}

public enum ProjectileHitPolicy
{
    OncePerProjectile,
    OncePerVolley,
}

[CreateAssetMenu(
    fileName = "ProjectileWeaponAttack",
    menuName = "Weapon/Attack/Projectile"
)]
public sealed class ProjectileWeaponAttackDefinition : WeaponAttackDefinition
{
    [Header("Projectile")]
    public ProjectileDefinition projectile;

    [Header("Shot / Burst")]
    public ProjectileShotMode shotMode = ProjectileShotMode.Shot;
    public ProjectileSpawnPattern spawnPattern = ProjectileSpawnPattern.Forward;

    [Min(1)]
    public int projectileCount = 1;

    [Range(0f, 360f)]
    public float coneAngle = 30f;

    [Tooltip("Base delay in seconds between projectiles when Shot Mode is Burst. Haste shortens this delay.")]
    [Min(0f)]
    public float burstShotInterval = 0.15f;

    [Tooltip("Total number of burst groups, including the first group, when Shot Mode is Burst Sequence.")]
    [Min(1)]
    public int burstRepeatCount = 2;

    [Tooltip("Base delay after the last projectile of one group before the first projectile of the next group. Haste shortens this delay.")]
    [Min(0f)]
    public float burstRepeatInterval = 0.75f;

    [Min(0f)]
    public float perProjectileDamageMultiplier = 1f;

    public ProjectileHitPolicy hitPolicy = ProjectileHitPolicy.OncePerProjectile;

    public int ResolveProjectileCount()
    {
        return shotMode == ProjectileShotMode.Shot ? 1 : Mathf.Max(1, projectileCount);
    }

    public int ResolveBurstCount()
    {
        return shotMode == ProjectileShotMode.BurstSequence
            ? Mathf.Max(1, burstRepeatCount)
            : 1;
    }

    public int ResolveTotalProjectileCount()
    {
        return ResolveProjectileCount() * ResolveBurstCount();
    }

}
