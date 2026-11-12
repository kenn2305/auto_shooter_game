using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AnimationHitboxTrack))]
public sealed class WeaponAttackActor : WeaponAttackActorBase
{
    [Header("Components")]
    [SerializeField] private AnimationHitboxTrack hitboxTrack;
    [SerializeField] private Animator animator;

    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float cleanupDelay = 0.05f;

    [SerializeField, Min(0f)]
    [Tooltip("Keeps the attack alive at least this many seconds (0 = as long as its animation). " +
        "When longer than the animation, the clip is replayed from Loop From until the time is up.")]
    private float minimumLifetime;

    [SerializeField, Range(0f, 0.99f)]
    [Tooltip("Normalized clip time the animation restarts from while Minimum Lifetime keeps the attack alive " +
        "(skip the wind-up frames).")]
    private float loopFromNormalizedTime;

    private readonly HashSet<IDamageReceiver> attackedTargets = new();

    private GameObject owner;
    private WeaponData weapon;
    private WeaponLevelData level;
    private CharacterCombatSystem combatStats;
    private bool initialized;

    private void Reset()
    {
        hitboxTrack = GetComponent<AnimationHitboxTrack>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (hitboxTrack == null)
        {
            hitboxTrack = GetComponent<AnimationHitboxTrack>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void OnEnable()
    {
        if (hitboxTrack != null)
        {
            hitboxTrack.ContactDetected += HandleContactDetected;
        }
    }

    private void OnDisable()
    {
        if (hitboxTrack != null)
        {
            hitboxTrack.ContactDetected -= HandleContactDetected;
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
        if (initialized)
        {
            return;
        }

        if (hitboxTrack == null || animator == null)
        {
            Debug.LogError(
                "WeaponAttackActor requires AnimationHitboxTrack and Animator components.",
                this
            );
            Destroy(gameObject);
            return;
        }

        owner = attackOwner;
        weapon = weaponData;
        level = levelData;
        combatStats = stats;
        animator.speed = Mathf.Max(0.01f, animationSpeed);

        ApplyAreaScale(weapon, combatStats);

        attackedTargets.Clear();
        hitboxTrack.ConfigureDetection(weapon.targetLayerMask, 1f);

        initialized = true;
        PlayAnimation();
        ScheduleDestroy();
    }

    private void HandleContactDetected(AnimationHitboxContact contact)
    {
        if (
            !initialized
            || !DamageReceiverResolver.TryResolve(contact.Collider, out IDamageReceiver target)
            || !target.IsAlive || IsOwner(target) || attackedTargets.Contains(target)
        )
        {
            return;
        }

        int hitLimit = level.attack.hitLimit;
        if (hitLimit >= 0 && attackedTargets.Count >= hitLimit)
        {
            return;
        }

        attackedTargets.Add(target);

        Vector3 hitPosition = ResolveHitPosition(contact.Collider, target);
        DamagePacket damage = WeaponDamageCalculator.Calculate(
            owner,
            weapon,
            combatStats,
            hitPosition,
            DamageType.Weapon,
            level.attack.GetDamageMultiplierAgainst(target)
        );

        DamageReceipt receipt = target.ReceiveDamage(damage);
        if (!receipt.Accepted)
        {
            return;
        }

        ReportHit(
            new WeaponHitResult(
                target,
                ResolveTargetObject(target),
                weapon,
                level,
                damage,
                hitPosition,
                (hitPosition - owner.transform.position).normalized,
                receipt,
                WeaponEffectOrigin.PrimaryAttack,
                WeaponProcFlags.PrimaryHit,
                chainDepth: 0
            )
        );
    }

    private void PlayAnimation()
    {
        if (level?.attack == null || string.IsNullOrWhiteSpace(level.attack.animatorStateName))
        {
            return;
        }

        animator.Play(level.attack.animatorStateName, 0, 0f);
    }

    private void ScheduleDestroy()
    {
        float animationDuration =
            hitboxTrack.AnimationClip != null ? hitboxTrack.AnimationClip.length / animator.speed : 0f;

        loopsAnimation = minimumLifetime > animationDuration;
        Destroy(gameObject, Mathf.Max(animationDuration, minimumLifetime) + cleanupDelay);
    }

    private bool loopsAnimation;

    private void Update()
    {
        if (!initialized || !loopsAnimation || animator == null || string.IsNullOrWhiteSpace(level?.attack?.animatorStateName))
        {
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.normalizedTime >= 1f)
        {
            animator.Play(level.attack.animatorStateName, 0, loopFromNormalizedTime);
        }
    }

    private bool IsOwner(IDamageReceiver target)
    {
        if (owner == null || target is not Component component)
        {
            return false;
        }

        return component.transform == owner.transform || component.transform.IsChildOf(owner.transform);
    }

    private Vector3 ResolveHitPosition(Component collider, IDamageReceiver target)
    {
        if (collider is Collider2D collider2D)
        {
            return collider2D.bounds.center;
        }

        if (collider is Collider collider3D)
        {
            return collider3D.bounds.center;
        }

        return target is Component component ? component.transform.position : transform.position;
    }

    private static GameObject ResolveTargetObject(IDamageReceiver target)
    {
        return target is Component component ? component.gameObject : null;
    }
}
