using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

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
    private readonly HashSet<IDamageReceiver> damagedTarget = new();

    private void Reset()
    {
        damageArea = GetComponent<CircleCollider2D>();
        if (damageArea != null) damageArea.isTrigger = true;
    }

    private void Awake()
    {
        if (damageArea != null) return;
        damageArea = GetComponent<CircleCollider2D>();

        damageArea.isTrigger = true;
    }

    public override void Initialize(
        WeaponEffectContext context,
        WeaponTriggeredEffectData effectData)
    {
        if (context.Weapon == null || context.Level == null || effectData == null)
        {
            Debug.LogError("Data invalid", this);
            Destroy(gameObject);
            return;
        }

        transform.position = context.Position;

        ContactFilter2D filter = new ContactFilter2D();

        filter.SetLayerMask(
            context.Weapon.targetLayerMask
        );

        filter.useLayerMask = true;
        filter.useTriggers = true;

        overlapResults.Clear();
        damagedTarget.Clear();

        damageArea.Overlap(filter, overlapResults);

        foreach (Collider2D collider in overlapResults)
        {
            if (!DamageReceiverResolver.TryResolve(
                    collider,
                    out IDamageReceiver target))
            {
                continue;
            }

            if (!target.IsAlive) continue;
            if (IsOwner(target, context.Owner)) continue;
            if (!damagedTarget.Add(target)) continue;

            Vector2 hitPosition = collider.bounds.center;

            DamagePacket damage =
                MeleeWeaponDamageCalculator.Calculate(
                    context.Owner,
                    context.Weapon,
                    context.Level,
                    context.Stats,
                    hitPosition,
                    DamageType.Area,
                    effectData.damageMutiplier
                );

            DamageReceipt receipt = target.ReceiveDamage(damage);

            if (!receipt.Accepted) continue;


            if (logHits)
            {
                Debug.Log(
                    $"[DEATH EXPLOSION HIT] " +
                    $"Target={collider.name} | " +
                    $"Damage={receipt.AppliedAmount} | " +
                    $"Killed={receipt.KilledTarget}",
                    this
                );
            }

            ReportHit(
                new MeleeHitResult(
                    target,
                    damage,
                    hitPosition,
                    receipt
                )
            );
        }

        Destroy(gameObject, visualLifetime);
    }

    private static bool IsOwner(IDamageReceiver target, GameObject owner)
    {
        if (owner == null || target is not Component component) return false;

        return component.transform.root == owner.transform.root;
    }
}
