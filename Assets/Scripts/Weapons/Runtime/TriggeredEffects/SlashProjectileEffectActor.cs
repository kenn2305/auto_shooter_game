using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class SlashProjectileEffectActor : WeaponTriggeredEffectActor
{
    [SerializeField, Min(0f)] private float speed = 10f;

    [SerializeField, Min(0f)]
    [FormerlySerializedAs("lifeTime")]
    private float lifetime = 3f;
    [SerializeField] private Rigidbody2D body;

    private readonly HashSet<IDamageReceiver> hitTargets = new();

    private WeaponEffectContext context;
    private WeaponTriggeredEffectData effectData;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    public override void Initialize(WeaponEffectContext effectContext, WeaponTriggeredEffectData triggeredEffectData)
    {
        if (body == null || effectContext.Weapon == null || effectContext.Level == null || triggeredEffectData == null)
        {
            Debug.LogError(
                "SlashProjectileEffectActor received incomplete component or effect data.",
                this
            );
            Destroy(gameObject);
            return;
        }

        context = effectContext;
        effectData = triggeredEffectData;

        Vector2 direction = context.Direction.sqrMagnitude > 0f ? context.Direction.normalized : Vector2.up;

        transform.SetPositionAndRotation(context.Position, Quaternion.identity);
        transform.right = direction;
        body.linearVelocity = direction * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        int targetLayer = 1 << other.gameObject.layer;
        if (
            (context.Weapon.targetLayerMask.value & targetLayer) == 0
            || !DamageReceiverResolver.TryResolve(other, out IDamageReceiver target)
            || !target.IsAlive || !hitTargets.Add(target)
        )
        {
            return;
        }

        Vector2 hitPosition = other.bounds.center;
        DamagePacket damage = WeaponDamageCalculator.Calculate(
            context.Owner,
            context.Weapon,
            context.Level,
            context.Stats,
            hitPosition,
            DamageType.Projectile,
            effectData.damageMultiplier
        );

        DamageReceipt receipt = target.ReceiveDamage(damage);
        if (receipt.Accepted)
        {
            ReportHit(
                new WeaponHitResult(
                    target,
                    target is Component component ? component.gameObject : null,
                    context.Weapon,
                    context.Level,
                    damage,
                    hitPosition,
                    context.Direction,
                    receipt,
                    context.Origin,
                    context.ProcFlags,
                    context.ChainDepth
                )
            );
        }
    }
}
