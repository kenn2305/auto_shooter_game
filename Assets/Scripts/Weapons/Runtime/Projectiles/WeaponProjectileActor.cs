using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponProjectileActor : MonoBehaviour
{
    public event Action<WeaponHitResult> HitResolved;
    public event Action<WeaponProjectileActor> Completed;

    private readonly HashSet<IDamageReceiver> hitTargets = new();
    private readonly HashSet<IDamageReceiver> queryTargets = new();

    private WeaponProjectileActor sourcePrefab;
    private GameObject owner;
    private WeaponData weapon;
    private WeaponLevelData level;
    private CharacterCombatSystem stats;
    private ProjectileWeaponAttackDefinition attackDefinition;
    private ProjectileDefinition definition;
    private ProjectileVolleyRuntimeState volleyState;

    private Vector2 startPoint;
    private Vector2 targetPoint;
    private Vector2 controlPoint;
    private Vector2 currentDirection;
    private Component bounceTarget;
    private ProjectileTrajectoryMode resolvedTrajectory;

    private float elapsedLifetime;
    private float elapsedTrajectory;
    private float trajectoryDuration;
    private float travelledDistance;
    private float areaMultiplier = 1f;
    private int bounceCount;
    private int piercedTargetCount;
    private bool initialized;
    private bool completed;

    internal void SetPoolSource(WeaponProjectileActor prefab)
    {
        sourcePrefab = prefab;
    }

    public void Initialize(
        GameObject attackOwner,
        WeaponData weaponData,
        WeaponLevelData levelData,
        CharacterCombatSystem combatStats,
        ProjectileWeaponAttackDefinition projectileAttack,
        Vector2 origin,
        Vector2 aimPoint,
        Vector2 direction,
        ProjectileTrajectorySnapshot trajectory,
        ProjectileVolleyRuntimeState sharedVolleyState
    )
    {
        ResetRuntimeState();

        owner = attackOwner;
        weapon = weaponData;
        level = levelData;
        stats = combatStats;
        attackDefinition = projectileAttack;
        definition = projectileAttack != null ? projectileAttack.projectile : null;
        volleyState = sharedVolleyState;

        if (owner == null || weapon == null || level == null || definition == null)
        {
            Debug.LogError("WeaponProjectileActor received incomplete launch data.", this);
            Complete();
            return;
        }

        areaMultiplier = WeaponAreaUtility.ResolveMultiplier(level, stats);
        Vector3 authoredScale = sourcePrefab != null ? sourcePrefab.transform.localScale : Vector3.one;
        transform.localScale = authoredScale * areaMultiplier;
        startPoint = origin;
        currentDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        resolvedTrajectory = trajectory.Mode;

        float aimDistance = Mathf.Clamp(
            Vector2.Distance(origin, aimPoint),
            definition.minimumAimDistance,
            definition.maximumRange
        );
        targetPoint = origin + currentDirection * aimDistance;
        Vector2 perpendicular = new Vector2(-currentDirection.y, currentDirection.x);
        controlPoint = (origin + targetPoint) * 0.5f + perpendicular * trajectory.ArcHeight * trajectory.ArcSign;
        trajectoryDuration = Mathf.Max(0.01f, EstimateTrajectoryLength() / definition.speed);

        transform.position = origin;
        transform.right = currentDirection;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || completed)
        {
            return;
        }

        elapsedLifetime += Time.fixedDeltaTime;
        if (elapsedLifetime >= definition.lifetime || travelledDistance >= definition.maximumRange)
        {
            CompleteAtPathEnd(transform.position);
            return;
        }

        Vector2 currentPosition = transform.position;
        Vector2 nextPosition = CalculateNextPosition(currentPosition);
        Vector2 displacement = nextPosition - currentPosition;
        float distance = displacement.magnitude;

        if (distance <= 0.00001f)
        {
            if (
                bounceTarget == null && (
                    resolvedTrajectory == ProjectileTrajectoryMode.Parabolic
                    || definition.impactMode == ProjectileImpactMode.ExplodeAtEnd
                )
            )
            {
                CompleteAtPathEnd(currentPosition);
            }
            return;
        }

        currentDirection = displacement / distance;
        transform.right = currentDirection;

        bool impactConsumedProjectile = SweepForImpacts(currentPosition, currentDirection, distance);
        if (impactConsumedProjectile || completed)
        {
            return;
        }

        transform.position = nextPosition;
        travelledDistance += distance;

        bool reachedParabolicEnd =
            resolvedTrajectory == ProjectileTrajectoryMode.Parabolic && elapsedTrajectory >= trajectoryDuration;
        bool reachedExplicitEnd =
            definition.impactMode == ProjectileImpactMode.ExplodeAtEnd
            && Vector2.SqrMagnitude(nextPosition - targetPoint) <= 0.000001f;
        if (bounceTarget == null && (reachedParabolicEnd || reachedExplicitEnd))
        {
            CompleteAtPathEnd(nextPosition);
        }
    }

    private Vector2 CalculateNextPosition(Vector2 currentPosition)
    {
        if (bounceTarget != null)
        {
            if (!TryGetLivingTarget(bounceTarget, out IDamageReceiver _))
            {
                bounceTarget = null;
                Complete();
                return currentPosition;
            }

            Vector2 destination = bounceTarget.transform.position;
            return Vector2.MoveTowards(currentPosition, destination, definition.speed * Time.fixedDeltaTime);
        }

        if (resolvedTrajectory == ProjectileTrajectoryMode.Straight)
        {
            if (definition.impactMode == ProjectileImpactMode.ExplodeAtEnd)
            {
                return Vector2.MoveTowards(currentPosition, targetPoint, definition.speed * Time.fixedDeltaTime);
            }

            return currentPosition + currentDirection * definition.speed * Time.fixedDeltaTime;
        }

        elapsedTrajectory = Mathf.Min(trajectoryDuration, elapsedTrajectory + Time.fixedDeltaTime);
        float t = Mathf.Clamp01(elapsedTrajectory / trajectoryDuration);
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * startPoint + 2f * oneMinusT * t * controlPoint + t * t * targetPoint;
    }

    private bool SweepForImpacts(Vector2 origin, Vector2 direction, float distance)
    {
        int combinedMask = weapon.targetLayerMask.value | definition.obstacleLayerMask.value;
        if (combinedMask == 0)
        {
            return false;
        }

        float collisionRadius = definition.collisionRadius * areaMultiplier;
        RaycastHit2D[] hits = collisionRadius > 0f
            ? Physics2D.CircleCastAll(origin, collisionRadius, direction, distance, combinedMask)
            : Physics2D.RaycastAll(origin, direction, distance, combinedMask);

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            int layerBit = 1 << hit.collider.gameObject.layer;
            bool isTargetLayer = (weapon.targetLayerMask.value & layerBit) != 0;
            if (
                isTargetLayer
                && DamageReceiverResolver.TryResolve(hit.collider, out IDamageReceiver target)
                && target.IsAlive && !IsOwner(target)
            )
            {
                Vector2 hitPosition = hit.point != Vector2.zero ? hit.point : (Vector2)hit.collider.bounds.center;
                if (HandleTargetImpact(target, hit.collider, hitPosition))
                {
                    return true;
                }

                continue;
            }

            if ((definition.obstacleLayerMask.value & layerBit) != 0)
            {
                if (definition.impactMode == ProjectileImpactMode.ExplodeAtEnd)
                {
                    Explode(hit.point);
                }
                else
                {
                    SpawnVfx(definition.impactVfxPrefab, hit.point);
                }
                Complete();
                return true;
            }
        }

        return false;
    }

    private bool HandleTargetImpact(IDamageReceiver target, Collider2D collider, Vector2 hitPosition)
    {
        switch (definition.impactMode)
        {
            case ProjectileImpactMode.TargetBounce:
                ApplyDirectHit(
                    target,
                    collider,
                    hitPosition,
                    Mathf.Pow(definition.damageMultiplierPerBounce, bounceCount)
                );
                SpawnVfx(definition.impactVfxPrefab, hitPosition);

                if (bounceCount >= definition.maximumBounces)
                {
                    Complete();
                    return true;
                }

                Component nextTarget = FindNextBounceTarget(hitPosition, target);
                if (nextTarget == null)
                {
                    Complete();
                    return true;
                }

                bounceCount++;
                bounceTarget = nextTarget;
                transform.position = hitPosition;
                return true;

            case ProjectileImpactMode.PierceAll:
                if (
                    ApplyDirectHit(
                        target,
                        collider,
                        hitPosition,
                        Mathf.Pow(definition.damageMultiplierPerPierce, piercedTargetCount)
                    )
                )
                {
                    piercedTargetCount++;
                    SpawnVfx(definition.impactVfxPrefab, hitPosition);
                }

                if (definition.maximumPiercedTargets >= 0 && piercedTargetCount >= definition.maximumPiercedTargets)
                {
                    Complete();
                    return true;
                }

                return false;

            case ProjectileImpactMode.ExplodeOnFirstEnemy:
                Explode(hitPosition);
                Complete();
                return true;

            case ProjectileImpactMode.ExplodeAtEnd:
                return false;

            default:
                return false;
        }
    }

    private bool ApplyDirectHit(
        IDamageReceiver target,
        Collider2D collider,
        Vector2 hitPosition,
        float impactMultiplier
    )
    {
        if (!TryClaimTarget(target))
        {
            return false;
        }

        float damageMultiplier = attackDefinition.perProjectileDamageMultiplier * impactMultiplier;
        DamagePacket damage = WeaponDamageCalculator.Calculate(
            owner,
            weapon,
            level,
            stats,
            hitPosition,
            DamageType.Projectile,
            damageMultiplier
        );

        DamageReceipt receipt = target.ReceiveDamage(damage);
        if (!receipt.Accepted)
        {
            return false;
        }

        HitResolved?.Invoke(
            new WeaponHitResult(
                target,
                ResolveTargetObject(target, collider),
                weapon,
                level,
                damage,
                hitPosition,
                currentDirection,
                receipt,
                WeaponEffectOrigin.PrimaryAttack,
                WeaponProcFlags.PrimaryHit,
                chainDepth: 0
            )
        );
        return true;
    }

    private void Explode(Vector2 explosionPoint)
    {
        SpawnVfx(definition.explosionVfxPrefab, explosionPoint);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            explosionPoint,
            definition.explosionRadius * areaMultiplier,
            weapon.targetLayerMask
        );

        queryTargets.Clear();
        int acceptedTargetCount = 0;
        foreach (Collider2D collider in colliders)
        {
            if (
                !DamageReceiverResolver.TryResolve(collider, out IDamageReceiver target)
                || !target.IsAlive || IsOwner(target) || !queryTargets.Add(target) || !TryClaimTarget(target)
            )
            {
                continue;
            }

            Vector2 hitPosition = collider.bounds.center;
            float damageMultiplier =
                attackDefinition.perProjectileDamageMultiplier * definition.explosionDamageMultiplier;
            DamagePacket damage = WeaponDamageCalculator.Calculate(
                owner,
                weapon,
                level,
                stats,
                hitPosition,
                DamageType.Area,
                damageMultiplier
            );

            DamageReceipt receipt = target.ReceiveDamage(damage);
            if (!receipt.Accepted)
            {
                continue;
            }

            Vector2 direction = hitPosition - explosionPoint;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = currentDirection;
            }

            HitResolved?.Invoke(
                new WeaponHitResult(
                    target,
                    ResolveTargetObject(target, collider),
                    weapon,
                    level,
                    damage,
                    hitPosition,
                    direction.normalized,
                    receipt,
                    WeaponEffectOrigin.PrimaryAttack,
                    WeaponProcFlags.PrimaryHit,
                    chainDepth: 0
                )
            );

            acceptedTargetCount++;
            if (definition.maximumExplosionTargets >= 0 && acceptedTargetCount >= definition.maximumExplosionTargets)
            {
                break;
            }
        }
    }

    private Component FindNextBounceTarget(Vector2 center, IDamageReceiver currentTarget)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            center,
            definition.bounceRadius * areaMultiplier,
            weapon.targetLayerMask
        );

        queryTargets.Clear();
        Component nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        foreach (Collider2D collider in colliders)
        {
            if (
                !DamageReceiverResolver.TryResolve(collider, out IDamageReceiver candidate)
                || !candidate.IsAlive
                || ReferenceEquals(candidate, currentTarget)
                || IsOwner(candidate)
                || !queryTargets.Add(candidate)
                || (definition.preventRepeatTargets && hitTargets.Contains(candidate))
                || candidate is not Component component
            )
            {
                continue;
            }

            float distanceSquared = ((Vector2)component.transform.position - center).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = component;
            }
        }

        return nearest;
    }

    private bool TryClaimTarget(IDamageReceiver target)
    {
        if (!hitTargets.Add(target))
        {
            return false;
        }

        return attackDefinition.hitPolicy != ProjectileHitPolicy.OncePerVolley
            || volleyState == null || volleyState.TryClaim(target);
    }

    private bool IsOwner(IDamageReceiver target)
    {
        return owner != null && target is Component component && component.transform.root == owner.transform.root;
    }

    private static bool TryGetLivingTarget(Component component, out IDamageReceiver target)
    {
        return DamageReceiverResolver.TryResolve(component, out target) && target.IsAlive;
    }

    private static GameObject ResolveTargetObject(IDamageReceiver target, Component fallback)
    {
        return target is Component component ? component.gameObject : fallback != null ? fallback.gameObject : null;
    }

    private float EstimateTrajectoryLength()
    {
        if (resolvedTrajectory == ProjectileTrajectoryMode.Straight)
        {
            return Vector2.Distance(startPoint, targetPoint);
        }

        const int SampleCount = 12;
        float length = 0f;
        Vector2 previous = startPoint;
        for (int i = 1; i <= SampleCount; i++)
        {
            float t = i / (float)SampleCount;
            float oneMinusT = 1f - t;
            Vector2 point = oneMinusT * oneMinusT * startPoint
                + 2f * oneMinusT * t * controlPoint + t * t * targetPoint;
            length += Vector2.Distance(previous, point);
            previous = point;
        }

        return length;
    }

    private void SpawnVfx(GameObject prefab, Vector2 position)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        instance.transform.localScale *= areaMultiplier;
        Destroy(instance, definition.vfxLifetime);
    }

    private void CompleteAtPathEnd(Vector2 position)
    {
        if (completed)
        {
            return;
        }

        if (definition != null && definition.impactMode == ProjectileImpactMode.ExplodeAtEnd)
        {
            Explode(position);
        }

        Complete();
    }

    private void Complete()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        initialized = false;
        Completed?.Invoke(this);
        HitResolved = null;
        Completed = null;
        WeaponProjectilePool.Return(this, sourcePrefab);
    }

    private void ResetRuntimeState()
    {
        hitTargets.Clear();
        queryTargets.Clear();
        owner = null;
        weapon = null;
        level = null;
        stats = null;
        attackDefinition = null;
        definition = null;
        volleyState = null;
        bounceTarget = null;
        resolvedTrajectory = ProjectileTrajectoryMode.Straight;
        elapsedLifetime = 0f;
        elapsedTrajectory = 0f;
        trajectoryDuration = 0f;
        travelledDistance = 0f;
        areaMultiplier = 1f;
        bounceCount = 0;
        piercedTargetCount = 0;
        initialized = false;
        completed = false;
    }
}
