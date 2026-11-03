using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class DeathExplosionEffectActor : WeaponTriggeredEffectActor
{
    [Header("Detection")]
    [SerializeField]
    private CircleCollider2D damageArea;

    [Header("Lifetime")]
    [SerializeField, Min(0.01f)]
    private float visualLifetime = 0.35f;

    [Header("Debug")]
    [SerializeField]
    private bool logHits;

    private readonly List<Collider2D> overlapResults = new();
    private readonly HashSet<IDamageReceiver> damagedTargets = new();

    private void Reset()
    {
        damageArea = GetComponent<CircleCollider2D>();
        if (damageArea != null)
        {
            damageArea.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (damageArea == null)
        {
            damageArea = GetComponent<CircleCollider2D>();
        }

        if (damageArea != null)
        {
            damageArea.isTrigger = true;
        }
    }

    public override void Initialize(
        WeaponEffectContext context,
        WeaponTriggeredEffectData effectData
    )
    {
        if (
            damageArea == null
            || context.Weapon == null
            || context.Level == null
            || effectData == null
        )
        {
            Debug.LogError("DeathExplosionEffectActor received incomplete effect data.", this);
            Destroy(gameObject);
            return;
        }

        transform.position = context.Position;

        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        filter.SetLayerMask(context.Weapon.targetLayerMask);

        overlapResults.Clear();
        damagedTargets.Clear();
        damageArea.Overlap(filter, overlapResults);

        foreach (Collider2D hitCollider in overlapResults)
        {
            if (
                !DamageReceiverResolver.TryResolve(hitCollider, out IDamageReceiver target)
                || !target.IsAlive
                || IsOwner(target, context.Owner)
                || !damagedTargets.Add(target)
            )
            {
                continue;
            }

            Vector2 hitPosition = hitCollider.bounds.center;
            DamagePacket damage = WeaponDamageCalculator.Calculate(
                context.Owner,
                context.Weapon,
                context.Level,
                context.Stats,
                hitPosition,
                DamageType.Area,
                effectData.damageMultiplier
            );

            DamageReceipt receipt = target.ReceiveDamage(damage);
            if (!receipt.Accepted)
            {
                continue;
            }

            if (logHits)
            {
                Debug.Log(
                    $"[DEATH EXPLOSION HIT] Target={hitCollider.name} | "
                        + $"Damage={receipt.AppliedAmount} | Killed={receipt.KilledTarget}",
                    this
                );
            }

            ReportHit(
                new WeaponHitResult(
                    target,
                    target is Component component ? component.gameObject : null,
                    context.Weapon,
                    context.Level,
                    damage,
                    hitPosition,
                    (hitPosition - (Vector2)context.Position).normalized,
                    receipt,
                    context.Origin,
                    context.ProcFlags,
                    context.ChainDepth
                )
            );
        }

        Destroy(gameObject, visualLifetime);
    }

    private static bool IsOwner(IDamageReceiver target, GameObject owner)
    {
        return owner != null
            && target is Component component
            && component.transform.root == owner.transform.root;
    }
}
