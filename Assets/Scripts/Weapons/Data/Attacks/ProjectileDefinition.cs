using UnityEngine;

public enum ProjectileTrajectoryMode
{
    Straight,
    Parabolic,
}

public enum ProjectileArcSide
{
    Left,
    Right,
    Alternate,
    Random,
}

public enum ProjectileImpactMode
{
    TargetBounce,
    PierceAll,
    ExplodeOnFirstEnemy,
    ExplodeAtEnd,
}

[CreateAssetMenu(fileName = "ProjectileDefinition", menuName = "Weapon/Projectile/Definition")]
public sealed class ProjectileDefinition : ScriptableObject
{
    [Header("Prefab")]
    public WeaponProjectileActor projectilePrefab;

    [Header("Movement")]
    [Min(0.01f)] public float speed = 12f;
    [Min(0.01f)] public float lifetime = 4f;
    [Min(0.01f)] public float maximumRange = 20f;
    [Min(0f)] public float collisionRadius = 0.1f;

    [Tooltip("Keeps the authored size: Area does not change the projectile, its collision, explosion or VFX.")]
    public bool ignoreAreaScaling;

    [Tooltip("Layers that stop the projectile. Enemy layers come from WeaponData.targetLayerMask.")]
    public LayerMask obstacleLayerMask;

    [Header("Trajectory")]
    [Tooltip("Straight luôn bay thẳng. Parabolic dùng một đường Bezier cố định từ nòng súng tới điểm ngắm.")]
    public ProjectileTrajectoryMode trajectory = ProjectileTrajectoryMode.Straight;

    [Tooltip("Chỉ dùng cho Parabolic. Khi bật, mỗi projectile có thể chốt thành đường thẳng theo Straight Path Chance. Lựa chọn chỉ diễn ra một lần lúc spawn.")]
    public bool randomizeStraightOrCurve;

    [Range(0f, 1f)]
    [Tooltip("Xác suất một projectile Parabolic được chốt thành đường thẳng. 0 = luôn cong, 1 = luôn thẳng.")]
    public float straightPathChance;

    [Min(0f)]
    [Tooltip("Độ lệch của đường cong so với đường thẳng khi không random Arc Height.")]
    public float arcHeight = 2f;

    [Tooltip("Khi bật, mỗi projectile chốt một Arc Height riêng trong khoảng Min/Max lúc spawn.")]
    public bool randomizeArcHeight;

    [Min(0f)]
    [Tooltip("Độ cong nhỏ nhất khi Randomize Arc Height được bật.")]
    public float minimumArcHeight = 1f;

    [Min(0f)]
    [Tooltip("Độ cong lớn nhất khi Randomize Arc Height được bật.")]
    public float maximumArcHeight = 3f;

    [Tooltip("Phía cong được chốt một lần lúc spawn: Left/Right cố định, Alternate luân phiên theo vị trí trong loạt, Random chọn trái hoặc phải cho từng viên.")]
    public ProjectileArcSide arcSide = ProjectileArcSide.Alternate;
    [Min(0.01f)] public float minimumAimDistance = 1f;

    [Header("Impact")]
    public ProjectileImpactMode impactMode = ProjectileImpactMode.PierceAll;

    [Header("Target Bounce")]
    [Min(0f)] public float bounceRadius = 4f;
    [Min(0)] public int maximumBounces = 3;
    [Range(0f, 2f)] public float damageMultiplierPerBounce = 0.8f;

    [Tooltip("When enabled, a target already hit by this projectile cannot be selected again.")]
    public bool preventRepeatTargets = true;

    [Header("Piercing")]
    [Tooltip("-1 means unlimited targets.")]
    [Min(-1)] public int maximumPiercedTargets = -1;
    [Range(0f, 2f)] public float damageMultiplierPerPierce = 1f;

    [Header("Explosion")]
    [Min(0.01f)] public float explosionRadius = 2.5f;
    [Min(0f)] public float explosionDamageMultiplier = 1f;

    [Tooltip("-1 means unlimited targets inside the explosion.")]
    [Min(-1)] public int maximumExplosionTargets = -1;

    [Tooltip("Optional zone created where the projectile explodes. Use with an Explode impact mode.")]
    public WeaponGroundZoneActor groundZonePrefab;

    [Header("Optional VFX")]
    public GameObject impactVfxPrefab;
    public GameObject explosionVfxPrefab;
    [Min(0.01f)] public float vfxLifetime = 1f;
}
