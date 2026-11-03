using UnityEngine;

public readonly struct ProjectileTrajectorySnapshot
{
    public ProjectileTrajectoryMode Mode { get; }
    public float ArcSign { get; }
    public float ArcHeight { get; }

    public ProjectileTrajectorySnapshot(ProjectileTrajectoryMode mode, float arcSign, float arcHeight)
    {
        Mode = mode;
        ArcSign = arcSign < 0f ? -1f : 1f;
        ArcHeight = Mathf.Max(0f, arcHeight);
    }
}

public readonly struct ProjectileLaunchContext
{
    public GameObject Owner { get; }
    public WeaponData Weapon { get; }
    public WeaponLevelData Level { get; }
    public CharacterCombatSystem Stats { get; }
    public ProjectileWeaponAttackDefinition Attack { get; }
    public Vector2 Origin { get; }
    public Vector2 AimPoint { get; }
    public Vector2 Direction { get; }
    public ProjectileTrajectorySnapshot Trajectory { get; }
    public ProjectileVolleyRuntimeState VolleyState { get; }

    public ProjectileLaunchContext(
        GameObject owner,
        WeaponData weapon,
        WeaponLevelData level,
        CharacterCombatSystem stats,
        ProjectileWeaponAttackDefinition attack,
        Vector2 origin,
        Vector2 aimPoint,
        Vector2 direction,
        ProjectileTrajectorySnapshot trajectory,
        ProjectileVolleyRuntimeState volleyState
    )
    {
        Owner = owner;
        Weapon = weapon;
        Level = level;
        Stats = stats;
        Attack = attack;
        Origin = origin;
        AimPoint = aimPoint;
        Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        Trajectory = trajectory;
        VolleyState = volleyState;
    }
}
