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

    [SerializeField, Min(0.01f)]
    [Tooltip("Playback speed of the projectile's animation (higher = snappier slash).")]
    private float animationSpeed = 1f;

    [SerializeField] private Rigidbody2D body;

    private readonly HashSet<IDamageReceiver> hitTargets = new();

    private WeaponEffectContext context;
    private WeaponTriggeredEffectData effectData;
    private Vector3 authoredScale;

    private float CurrentAreaMultiplier => WeaponAreaUtility.ResolveMultiplier(context.Weapon, context.Stats);

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
        authoredScale = transform.localScale / Mathf.Max(0.0001f, CurrentAreaMultiplier);

        Vector2 direction = context.Direction.sqrMagnitude > 0f ? context.Direction.normalized : Vector2.up;

        transform.SetPositionAndRotation(context.Position, Quaternion.identity);
        transform.right = direction;
        body.linearVelocity = direction * speed;

        foreach (Animator animator in GetComponentsInChildren<Animator>())
        {
            animator.speed = animationSpeed;
        }

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Every projectile in flight follows the current area, so none is left smaller or larger than the others.
        if (context.Weapon != null)
        {
            transform.localScale = authoredScale * CurrentAreaMultiplier;
        }
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
