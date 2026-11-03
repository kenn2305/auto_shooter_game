using UnityEngine;

public enum BeamMode
{
    Pulse,
    Continuous,
}

public enum BeamAimTracking
{
    SnapshotOnAttack,
    FollowCursor,
}

public enum BeamHitMode
{
    FirstTarget,
    PierceAll,
    LimitedTargets,
}

public enum BeamOnHitPolicy
{
    EveryDamageTick,
    OncePerActivation,
}

[CreateAssetMenu(fileName = "BeamWeaponAttack", menuName = "Weapon/Attack/Beam")]
public sealed class BeamWeaponAttackDefinition : WeaponAttackDefinition
{
    [Header("Beam")]
    public BeamMode beamMode = BeamMode.Pulse;
    public BeamAimTracking aimTracking = BeamAimTracking.SnapshotOnAttack;
    [Min(0.01f)] public float maximumRange = 12f;
    [Min(0f)] public float beamWidth = 0.1f;

    [Header("Timing")]
    [Min(0.01f)] public float duration = 0.15f;
    [Min(0.01f)] public float damageTickInterval = 0.2f;
    [Min(0f)] public float damageMultiplierPerTick = 1f;

    [Header("Hits")]
    public BeamHitMode hitMode = BeamHitMode.FirstTarget;
    [Min(1)] public int maximumTargets = 3;

    public BeamOnHitPolicy onHitPolicy = BeamOnHitPolicy.EveryDamageTick;
    public LayerMask obstacleLayerMask;

    [Header("Visual")]
    [Min(0.001f)] public float startWidth = 0.12f;
    [Min(0.001f)] public float endWidth = 0.06f;

    public Color startColor = Color.red;
    public Color endColor = new Color(1f, 0.25f, 0.1f, 0.35f);
    public Material beamMaterial;
    public GameObject muzzleVfxPrefab;
    public GameObject impactVfxPrefab;
    [Min(0.01f)] public float impactVfxLifetime = 0.5f;
}
