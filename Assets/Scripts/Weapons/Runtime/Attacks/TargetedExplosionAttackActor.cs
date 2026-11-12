using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Picks the nearest enemies visible to the camera and detonates one explosion on each,
/// in random order and one after another, then destroys itself.
/// </summary>
[DisallowMultipleComponent]
public sealed class TargetedExplosionAttackActor : WeaponAttackActorBase
{
    private readonly HashSet<IDamageReceiver> damagedTargets = new();

    private GameObject owner;
    private WeaponData weapon;
    private WeaponLevelData level;
    private CharacterCombatSystem stats;
    private TargetedExplosionAttackDefinition definition;
    private float areaMultiplier;
    private bool initialized;

    public override void Initialize(
        GameObject attackOwner,
        WeaponData weaponData,
        WeaponLevelData levelData,
        CharacterCombatSystem combatStats,
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
            || weaponData == null
            || levelData?.attack is not TargetedExplosionAttackDefinition attackDefinition
            || attackDefinition.explosionPrefab == null
        )
        {
            Debug.LogError("TargetedExplosionAttackActor requires a TargetedExplosionAttackDefinition.", this);
            Destroy(gameObject);
            return;
        }

        owner = attackOwner;
        weapon = weaponData;
        level = levelData;
        stats = combatStats;
        definition = attackDefinition;
        areaMultiplier = WeaponAreaUtility.ResolveMultiplier(weapon, stats);

        List<ExplosionTarget> targets = FindTargets();
        if (targets.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(DetonateTargets(targets, definition.explosionInterval / Mathf.Max(0.01f, animationSpeed)));
    }

    private IEnumerator DetonateTargets(List<ExplosionTarget> targets, float interval)
    {
        WaitForSeconds wait = new(interval);
        for (int i = 0; i < targets.Count; i++)
        {
            Detonate(targets[i].CurrentPosition);

            if (i < targets.Count - 1 && interval > 0f)
            {
                yield return wait;
            }
        }

        Destroy(gameObject);
    }

    private List<ExplosionTarget> FindTargets()
    {
        List<ExplosionTarget> visible = new();
        Camera camera = Camera.main;
        if (camera == null)
        {
            return visible;
        }

        float depth = Mathf.Abs(camera.transform.position.z - transform.position.z);
        Vector2 minimum = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector2 maximum = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        Rect view = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);

        HashSet<IDamageReceiver> seen = damagedTargets;
        seen.Clear();
        Vector2 origin = transform.position;
        using PhysicsQuery.Results<Collider2D> candidates = PhysicsQuery.OverlapArea(minimum, maximum, weapon.targetLayerMask);
        foreach (Collider2D collider in candidates.List)
        {
            Vector2 position = collider.bounds.center;
            if (
                view.Contains(position)
                && DamageReceiverResolver.TryResolve(collider, out IDamageReceiver receiver)
                && receiver.IsAlive && !IsOwner(receiver) && seen.Add(receiver)
            )
            {
                visible.Add(new ExplosionTarget(receiver, collider, position, (position - origin).sqrMagnitude));
            }
        }

        visible.Sort((left, right) => left.SquaredDistance.CompareTo(right.SquaredDistance));
        if (visible.Count > definition.explosionCount)
        {
            visible.RemoveRange(definition.explosionCount, visible.Count - definition.explosionCount);
        }

        for (int i = visible.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (visible[i], visible[swapIndex]) = (visible[swapIndex], visible[i]);
        }

        return visible;
    }

    private void Detonate(Vector2 center)
    {
        VfxPool.Spawn(definition.explosionPrefab, center, Quaternion.identity, definition.visualLifetime, areaMultiplier);

        damagedTargets.Clear();
        int hitLimit = definition.hitLimit;
        using PhysicsQuery.Results<Collider2D> colliders = PhysicsQuery.OverlapCircle(
            center,
            definition.explosionRadius * areaMultiplier,
            weapon.targetLayerMask
        );

        foreach (Collider2D collider in colliders.List)
        {
            if (hitLimit >= 0 && damagedTargets.Count >= hitLimit)
            {
                break;
            }

            if (
                !DamageReceiverResolver.TryResolve(collider, out IDamageReceiver target)
                || !target.IsAlive || IsOwner(target) || !damagedTargets.Add(target)
            )
            {
                continue;
            }

            Damage(target, collider, center);
        }
    }

    private void Damage(IDamageReceiver target, Collider2D collider, Vector2 center)
    {
        Vector2 hitPosition = collider.bounds.center;
        DamagePacket damage = WeaponDamageCalculator.Calculate(
            owner,
            weapon,
            stats,
            hitPosition,
            DamageType.Area,
            definition.GetDamageMultiplierAgainst(target)
        );

        DamageReceipt receipt = target.ReceiveDamage(damage);
        if (!receipt.Accepted)
        {
            return;
        }

        Vector2 direction = hitPosition - center;
        ReportHit(
            new WeaponHitResult(
                target,
                target is Component component ? component.gameObject : collider.gameObject,
                weapon,
                level,
                damage,
                hitPosition,
                direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up,
                receipt,
                WeaponEffectOrigin.PrimaryAttack,
                WeaponProcFlags.PrimaryHit,
                chainDepth: 0
            )
        );
    }

    private bool IsOwner(IDamageReceiver target)
    {
        return target is Component component && component.transform.root == owner.transform.root;
    }

    private readonly struct ExplosionTarget
    {
        private readonly IDamageReceiver receiver;
        private readonly Collider2D collider;
        private readonly Vector2 lastPosition;

        public float SquaredDistance { get; }

        // A target that died before its turn still explodes where it was last seen.
        public Vector2 CurrentPosition =>
            receiver.IsAlive && collider != null && collider.isActiveAndEnabled
                ? (Vector2)collider.bounds.center
                : lastPosition;

        public ExplosionTarget(IDamageReceiver receiver, Collider2D collider, Vector2 position, float squaredDistance)
        {
            this.receiver = receiver;
            this.collider = collider;
            lastPosition = position;
            SquaredDistance = squaredDistance;
        }
    }
}
