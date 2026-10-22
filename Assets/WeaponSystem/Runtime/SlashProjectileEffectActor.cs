using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class SlashProjectileEffectActor : WeaponTriggeredEffectActor
{
    [SerializeField, Min(0f)]
    private float speed = 10f;

    [SerializeField, Min(0f)]
    private float lifeTime = 3f;

    [SerializeField] private Rigidbody2D body;

    private WeaponEffectContext context;
    private WeaponTriggeredEffectData effectData;

    private readonly HashSet<IDamageReceiver> hitTarget = new();

    private void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
    }

    public override void Initialize(WeaponEffectContext context, WeaponTriggeredEffectData effectData)
    {
        this.context = context;
        this.effectData = effectData;

        Vector2 direction = context.Direction.normalized;
        transform.position = context.Position;
        transform.right = direction;

        body.linearVelocity = direction * speed;

        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        int targetLayer = 1 << other.gameObject.layer;

        if ((context.Weapon.targetLayerMask.value & targetLayer) == 0) return;

        if (!DamageReceiverResolver.TryResolve(
                other,
                out IDamageReceiver target)) return;

        if (!target.IsAlive) return;

        if (!hitTarget.Add(target)) return;

        Vector2 hitPosition = other.bounds.center;

        DamagePacket damage =
            MeleeWeaponDamageCalculator.Calculate(
                context.Owner,
                context.Weapon,
                context.Level,
                context.Stats,
                hitPosition,
                DamageType.Projectile,
                effectData.damageMutiplier
            );

        DamageReceipt receipt = target.ReceiveDamage(damage);

        if (!receipt.Accepted) return;

        ReportHit(
            new MeleeHitResult(
                target,
                damage,
                hitPosition,
                receipt
            )
        );
    }
}
