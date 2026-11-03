using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class WeaponAuraActor : WeaponAttackActorBase
{
    [SerializeField] private CircleCollider2D detectionArea;
    [SerializeField] private Transform visualRoot;

    private readonly List<Collider2D> overlapResults = new();
    private readonly HashSet<IDamageReceiver> hitTargets = new();

    private GameObject owner;
    private WeaponData weapon;
    private WeaponLevelData level;
    private CharacterCombatSystem combatStats;
    private ContactFilter2D contactFilter;
    private float baseRadius;
    private Vector3 baseVisualScale;
    private bool initialized;

    private void Reset()
    {
        detectionArea = GetComponent<CircleCollider2D>();
        if (detectionArea != null)
        {
            detectionArea.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (detectionArea == null)
        {
            detectionArea = GetComponent<CircleCollider2D>();
        }

        if (detectionArea != null)
        {
            baseRadius = detectionArea.radius;
            detectionArea.isTrigger = true;
        }

        if (visualRoot != null)
        {
            baseVisualScale = visualRoot.localScale;
        }
    }

    public override void Initialize(
        GameObject attackOwner,
        WeaponData weaponData,
        WeaponLevelData levelData,
        CharacterCombatSystem stats,
        float animationSpeed
    )
    {
        if (detectionArea == null || weaponData == null || levelData?.attack == null)
        {
            Debug.LogError("WeaponAuraActor received incomplete component or weapon data.", this);
            return;
        }

        owner = attackOwner;
        weapon = weaponData;
        combatStats = stats;

        contactFilter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        contactFilter.SetLayerMask(weapon.targetLayerMask);

        initialized = true;
        RefreshLevel(levelData);
    }

    public override void RefreshLevel(WeaponLevelData newLevel)
    {
        if (newLevel == null || detectionArea == null)
        {
            return;
        }

        level = newLevel;
        RefreshArea();
    }

    private void RefreshArea()
    {
        ApplyAreaScale(level, combatStats);
        detectionArea.radius = baseRadius;

        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.localScale = baseVisualScale;
        }
    }

    public override void ExecuteAttack()
    {
        if (!initialized || level?.attack == null)
        {
            return;
        }

        RefreshArea();
        overlapResults.Clear();
        hitTargets.Clear();
        detectionArea.Overlap(contactFilter, overlapResults);

        int acceptedHitCount = 0;
        int hitLimit = level.attack.hitLimit;

        foreach (Collider2D detectedCollider in overlapResults)
        {
            if (
                !DamageReceiverResolver.TryResolve(detectedCollider, out IDamageReceiver target)
                || !target.IsAlive || IsOwner(target) || !hitTargets.Add(target)
            )
            {
                continue;
            }

            if (hitLimit >= 0 && acceptedHitCount >= hitLimit)
            {
                break;
            }

            Vector2 hitPosition = detectedCollider.bounds.center;
            DamagePacket damage = WeaponDamageCalculator.Calculate(
                owner,
                weapon,
                level,
                combatStats,
                hitPosition,
                DamageType.Area
            );

            DamageReceipt receipt = target.ReceiveDamage(damage);
            if (!receipt.Accepted)
            {
                continue;
            }

            ReportHit(
                new WeaponHitResult(
                    target,
                    target is Component component ? component.gameObject : null,
                    weapon,
                    level,
                    damage,
                    hitPosition,
                    (hitPosition - (Vector2)owner.transform.position).normalized,
                    receipt,
                    WeaponEffectOrigin.PrimaryAttack,
                    WeaponProcFlags.PrimaryHit,
                    chainDepth: 0
                )
            );
            acceptedHitCount++;
        }
    }

    private bool IsOwner(IDamageReceiver target)
    {
        return owner != null && target is Component component && component.transform.root == owner.transform.root;
    }
}
