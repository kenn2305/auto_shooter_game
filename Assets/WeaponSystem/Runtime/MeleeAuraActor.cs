using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class MeleeAuraActor : MeleeAttackActorBase
{
    [SerializeField]
    private CircleCollider2D detectionArea;

    [SerializeField]
    private Transform visualRoot;

    private readonly List<Collider2D> results = new();
    private readonly HashSet<IDamageReceiver> hitTarget = new();

    private GameObject owner;
    private MeleeWeaponData weapon;
    private MeleeWeaponLevelData level;
    private CharacterCombatSystem combatStats;

    private ContactFilter2D contactFilter;

    private float baseRadius;
    private Vector3 baseVisualScale;
    private bool initialized;

    private void Reset()
    {
        detectionArea = GetComponent<CircleCollider2D>();

        if (detectionArea != null) detectionArea.isTrigger = true;
    }

    private void Awake()
    {
        if (detectionArea == null) detectionArea = GetComponent<CircleCollider2D>();
        if (detectionArea != null)
        {
            baseRadius = detectionArea.radius;
            detectionArea.isTrigger = true;
        }

        if (visualRoot != null) baseVisualScale = visualRoot.localScale;
    }

    public override void Initialize(
        GameObject attackOwner,
        MeleeWeaponData weapon,
        MeleeWeaponLevelData level,
        CharacterCombatSystem stats,
        float animationSpeed)
    {
        if (detectionArea == null)
            detectionArea = GetComponent<CircleCollider2D>();

        if (detectionArea == null)
        {
            Debug.LogError(
                "MeleeAuraActor requires a CircleCollider2D.",
                this
            );
            return;
        }

        if (weapon == null || level == null || level.attack == null)
        {
            Debug.LogError(
                "MeleeAuraActor received incomplete weapon data.",
                this
            );
            return;
        }

        owner = attackOwner;
        this.weapon = weapon;
        combatStats = stats;

        contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(
            weapon.targetLayerMask
        );
        contactFilter.useLayerMask = true;
        contactFilter.useTriggers = true;

        initialized = true;

        RefreshLevel(level);

    }

    public override void RefreshLevel(MeleeWeaponLevelData newLevel)
    {
        if (newLevel == null || detectionArea == null) return;

        level = newLevel;

        detectionArea.radius = baseRadius * level.areaMultiplier;

        if (visualRoot != null)
        {
            visualRoot.localScale = baseVisualScale * level.areaMultiplier;
        }
    }

    public override void ExecuteAttack()
    {
        if (!initialized || level == null || level.attack == null)
            return;

        results.Clear();
        hitTarget.Clear();

        detectionArea.Overlap(contactFilter, results);

        int acceptedHitCount = 0;
        int hitLimit = level.attack.hitLimit;

        for (int i = 0; i < results.Count; i++)
        {
            Collider2D detectedCollider = results[i];

            if (!DamageReceiverResolver.TryResolve(
                    detectedCollider,
                    out IDamageReceiver target))
                continue;

            if (!target.IsAlive || IsOwner(target))
                continue;

            if (!hitTarget.Add(target))
                continue;

            if (hitLimit >= 0 && acceptedHitCount >= hitLimit)
                break;

            Vector2 hitPosition = detectedCollider.bounds.center;

            DamagePacket damage =
                MeleeWeaponDamageCalculator.Calculate(
                    owner,
                    weapon,
                    level,
                    combatStats,
                    hitPosition,
                    DamageType.Area
                );

            DamageReceipt receipt = target.ReceiveDamage(damage);

            if (!receipt.Accepted)
                continue;

            ReportHit(
                new MeleeHitResult(
                    target,
                    damage,
                    hitPosition,
                    receipt
                )
            );

            acceptedHitCount++;
        }
    }

    private bool IsOwner(IDamageReceiver target)
    {
        if (owner == null ||
            target is not Component component)
        {
            return false;
        }

        return component.transform.root ==
               owner.transform.root;
    }
}

