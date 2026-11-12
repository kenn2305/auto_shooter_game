using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProjectileWeaponAttackActor : WeaponAttackActorBase
{
    private int activeProjectileCount;
    private int pendingProjectileCount;
    private bool spawnSequenceCompleted;
    private bool initialized;

    public override void Initialize(
        GameObject attackOwner,
        WeaponData weapon,
        WeaponLevelData level,
        CharacterCombatSystem stats,
        float animationSpeed
    )
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        if (
            attackOwner == null
            || weapon == null
            || level?.attack is not ProjectileWeaponAttackDefinition attackDefinition
            || attackDefinition.projectile == null || attackDefinition.projectile.projectilePrefab == null
        )
        {
            Debug.LogError(
                "ProjectileWeaponAttackActor requires a ProjectileWeaponAttackDefinition with a projectile prefab.",
                this
            );
            Destroy(gameObject);
            return;
        }

        WeaponController controller = ResolveController(attackOwner, weapon);
        WeaponAimSnapshot aim = controller != null
            ? controller.CaptureAimSnapshot()
            : new WeaponAimSnapshot(transform.position, transform.position + transform.up, transform.up);

        int projectilesPerBurst = attackDefinition.ResolveProjectileCount();
        int burstCount = attackDefinition.ResolveBurstCount();
        ProjectileVolleyRuntimeState volleyState =
            attackDefinition.hitPolicy == ProjectileHitPolicy.OncePerVolley ? new ProjectileVolleyRuntimeState() : null;

        pendingProjectileCount = attackDefinition.ResolveTotalProjectileCount();
        if (
            attackDefinition.shotMode == ProjectileShotMode.Burst
            || attackDefinition.shotMode == ProjectileShotMode.BurstSequence
        )
        {
            StartCoroutine(
                SpawnBurstSequence(
                    attackOwner,
                    weapon,
                    level,
                    stats,
                    attackDefinition,
                    aim,
                    projectilesPerBurst,
                    burstCount,
                    volleyState,
                    animationSpeed
                )
            );
            return;
        }

        for (int i = 0; i < projectilesPerBurst; i++)
        {
            SpawnProjectile(
                attackOwner,
                weapon,
                level,
                stats,
                attackDefinition,
                aim,
                projectilesPerBurst,
                i,
                i,
                volleyState
            );
            pendingProjectileCount--;
        }

        spawnSequenceCompleted = true;
        TryCompleteActor();
    }

    private IEnumerator SpawnBurstSequence(
        GameObject attackOwner,
        WeaponData weapon,
        WeaponLevelData level,
        CharacterCombatSystem stats,
        ProjectileWeaponAttackDefinition attackDefinition,
        WeaponAimSnapshot lockedAim,
        int projectilesPerBurst,
        int burstCount,
        ProjectileVolleyRuntimeState volleyState,
        float attackSpeedMultiplier
    )
    {
        float speedMultiplier = Mathf.Max(0.01f, attackSpeedMultiplier);
        float shotInterval = attackDefinition.burstShotInterval / Mathf.Max(0.01f, attackSpeedMultiplier);
        float repeatInterval = attackDefinition.burstRepeatInterval / speedMultiplier;
        int trajectoryIndex = 0;

        for (int burstIndex = 0; burstIndex < burstCount; burstIndex++)
        {
            for (int shotIndex = 0; shotIndex < projectilesPerBurst; shotIndex++)
            {
                SpawnProjectile(
                    attackOwner,
                    weapon,
                    level,
                    stats,
                    attackDefinition,
                    lockedAim,
                    projectilesPerBurst,
                    shotIndex,
                    trajectoryIndex,
                    volleyState
                );
                pendingProjectileCount--;
                trajectoryIndex++;

                if (shotIndex < projectilesPerBurst - 1 && shotInterval > 0f)
                {
                    yield return new WaitForSeconds(shotInterval);
                }
            }

            if (burstIndex < burstCount - 1 && repeatInterval > 0f)
            {
                yield return new WaitForSeconds(repeatInterval);
            }
        }

        spawnSequenceCompleted = true;
        TryCompleteActor();
    }

    private void SpawnProjectile(
        GameObject attackOwner,
        WeaponData weapon,
        WeaponLevelData level,
        CharacterCombatSystem stats,
        ProjectileWeaponAttackDefinition attackDefinition,
        WeaponAimSnapshot lockedAim,
        int projectilePatternCount,
        int patternIndex,
        int trajectoryIndex,
        ProjectileVolleyRuntimeState volleyState
    )
    {
        Vector2 origin = transform.position;
        Vector2 aimPoint = ResolveAimPoint(attackDefinition, origin, lockedAim);
        Vector2 baseDirection = aimPoint - origin;
        if (baseDirection.sqrMagnitude <= 0.0001f)
        {
            baseDirection = lockedAim.Direction;
        }

        float angleOffset = ResolveAngleOffset(attackDefinition, projectilePatternCount, patternIndex);
        Vector2 direction = Rotate(baseDirection.normalized, angleOffset);
        float aimDistance = Mathf.Clamp(
            Vector2.Distance(origin, aimPoint),
            attackDefinition.projectile.minimumAimDistance,
            attackDefinition.projectile.maximumRange
        );
        Vector2 targetPoint = origin + direction * aimDistance;

        WeaponProjectileActor projectile = WeaponProjectilePool.Rent(attackDefinition.projectile.projectilePrefab);
        if (projectile == null)
        {
            return;
        }

        activeProjectileCount++;
        projectile.HitResolved += HandleProjectileHit;
        projectile.Completed += HandleProjectileCompleted;
        projectile.Initialize(
            attackOwner,
            weapon,
            level,
            stats,
            attackDefinition,
            origin,
            targetPoint,
            direction,
            ResolveTrajectory(attackDefinition.projectile, trajectoryIndex),
            volleyState
        );
    }

    private void HandleProjectileHit(WeaponHitResult hit)
    {
        ReportHit(hit);
    }

    private void HandleProjectileCompleted(WeaponProjectileActor projectile)
    {
        if (projectile != null)
        {
            projectile.HitResolved -= HandleProjectileHit;
            projectile.Completed -= HandleProjectileCompleted;
        }

        activeProjectileCount = Mathf.Max(0, activeProjectileCount - 1);
        TryCompleteActor();
    }

    private void TryCompleteActor()
    {
        if (spawnSequenceCompleted && pendingProjectileCount <= 0 && activeProjectileCount == 0)
        {
            Destroy(gameObject);
        }
    }

    private static WeaponController ResolveController(GameObject owner, WeaponData weapon)
    {
        WeaponController[] controllers = owner.GetComponents<WeaponController>();
        foreach (WeaponController controller in controllers)
        {
            if (controller.Data == weapon)
            {
                return controller;
            }
        }

        return controllers.Length > 0 ? controllers[0] : null;
    }

    private static Vector2 ResolveAimPoint(
        ProjectileWeaponAttackDefinition definition,
        Vector2 origin,
        WeaponAimSnapshot aim
    )
    {
        if (definition.spawnPattern != ProjectileSpawnPattern.RandomLanding)
        {
            return aim.Point;
        }

        // Keep the whole spread inside the range so landings do not bunch up on the range limit.
        float minimum = definition.projectile.minimumAimDistance;
        float spread = definition.landingSpreadRadius;
        float maximumCenter = Mathf.Max(minimum, definition.projectile.maximumRange - spread);
        Vector2 offset = aim.Point - origin;
        Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : aim.Direction;
        float centerDistance = Mathf.Clamp(offset.magnitude, minimum, maximumCenter);

        return origin + direction * centerDistance + Random.insideUnitCircle * spread;
    }

    private static float ResolveAngleOffset(ProjectileWeaponAttackDefinition definition, int count, int index)
    {
        if (definition.spawnPattern != ProjectileSpawnPattern.Cone || count <= 1)
        {
            return 0f;
        }

        return Mathf.Lerp(-definition.coneAngle * 0.5f, definition.coneAngle * 0.5f, index / (count - 1f));
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos).normalized;
    }

    private static float ResolveArcSign(ProjectileDefinition definition, int projectileIndex)
    {
        return definition.arcSide switch
        {
            ProjectileArcSide.Left => 1f,
            ProjectileArcSide.Right => -1f,
            ProjectileArcSide.Alternate => projectileIndex % 2 == 0 ? 1f : -1f,
            ProjectileArcSide.Random => Random.value < 0.5f ? -1f : 1f,
            _ => 1f,
        };
    }

    private static ProjectileTrajectorySnapshot ResolveTrajectory(ProjectileDefinition definition, int projectileIndex)
    {
        ProjectileTrajectoryMode resolvedMode = definition.trajectory;
        if (
            resolvedMode == ProjectileTrajectoryMode.Parabolic
            && definition.randomizeStraightOrCurve && Random.value < definition.straightPathChance
        )
        {
            resolvedMode = ProjectileTrajectoryMode.Straight;
        }

        float resolvedArcHeight = definition.arcHeight;
        if (definition.randomizeArcHeight)
        {
            float minimum = Mathf.Min(definition.minimumArcHeight, definition.maximumArcHeight);
            float maximum = Mathf.Max(definition.minimumArcHeight, definition.maximumArcHeight);
            resolvedArcHeight = Random.Range(minimum, maximum);
        }

        return new ProjectileTrajectorySnapshot(
            resolvedMode,
            ResolveArcSign(definition, projectileIndex),
            resolvedArcHeight
        );
    }
}
