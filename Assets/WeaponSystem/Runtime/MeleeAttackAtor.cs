using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
[DisallowMultipleComponent]
[RequireComponent(typeof(AnimationHitboxTrack))]
public sealed class MeleeAttackAtor : MeleeAttackActorBase
{
    [Header("Components")]
    [SerializeField] private AnimationHitboxTrack hitboxTrack;

    [SerializeField]
    private Animator animator;

    [Header("Lifetime")]
    [SerializeField, Min(0f)]
    private float cleanupDelay = 0.05f;

    private GameObject owner;
    private MeleeWeaponData weapon;
    private MeleeWeaponLevelData level;
    private CharacterCombatSystem stats;

    private bool initialized;
    private int acceptedHitCount;

    private readonly HashSet<IDamageReceiver> attackTarget = new();

    private void Reset()
    {
        hitboxTrack = GetComponent<AnimationHitboxTrack>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (hitboxTrack == null)
        {
            hitboxTrack =
                GetComponent<AnimationHitboxTrack>();
        }

        if (animator == null)
        {
            animator =
                GetComponent<Animator>();
        }
    }

    private void OnEnable()
    {
        if (hitboxTrack == null) return;

        hitboxTrack.ContactDetected += HandleContactDetected;
    }

    private void OnDisable()
    {
        if (hitboxTrack == null)
        {
            return;
        }

        hitboxTrack.ContactDetected -= HandleContactDetected;
    }

    public override void Initialize(
        GameObject attackowner,
        MeleeWeaponData meleeWeaponData,
        MeleeWeaponLevelData levelData,
        CharacterCombatSystem combatStats,
        float animationSpeed)
    {
        if (initialized)
            return;

        owner = attackowner;
        weapon = meleeWeaponData;
        level = levelData;
        stats = combatStats;
        animator.speed = animationSpeed;

        attackTarget.Clear();

        hitboxTrack.ConfigureDetection(weapon.targetLayerMask, level.areaMultiplier);

        initialized = true;

        PlayAnimation();
        ScheduleDestroy();
    }

    private void PlayAnimation()
    {
        if (animator == null || level == null || level.attack == null) return;

        string stateName = level.attack.animatorStateName;

        if (string.IsNullOrWhiteSpace(stateName)) return;

        animator.Play(stateName, 0, 0f);
    }

    private void HandleContactDetected(AnimationHitboxContact contact)
    {
        if (!initialized) return;

        if (!DamageReceiverResolver.TryResolve(contact.Collider, out IDamageReceiver target)) return;

        if (target == null || !target.IsAlive || IsOwner(target)) return;


        if (attackTarget.Contains(target)) return;

        int hitLimit = level.attack.hitLimit;

        if (hitLimit >= 0 && attackTarget.Count >= hitLimit) return;

        attackTarget.Add(target);

        Vector3 hitPos;
        if (contact.Collider is Collider2D collider2D)
        {
            hitPos = collider2D.bounds.center;
        }
        else if (contact.Collider is Collider collider3D)
        {
            hitPos = collider3D.bounds.center;
        }
        else if (target is Component component)
        {
            hitPos = component.transform.position;
        }
        else
        {
            hitPos = transform.position;
        }

        DamagePacket damage =
            MeleeWeaponDamageCalculator.Calculate(
                owner,
                weapon,
                level,
                stats,
                hitPos,
                DamageType.Melee
            );

        DamageReceipt receipt = target.ReceiveDamage(damage);
        if (!receipt.Accepted) return;

        ReportHit(
            new MeleeHitResult(
                target,
                damage,
                hitPos,
                receipt
            )
        );
    }

    private bool IsOwner(
    IDamageReceiver target
)
    {
        if (owner == null ||
            target is not Component component)
        {
            return false;
        }

        return component.transform ==
               owner.transform ||
               component.transform.IsChildOf(
                   owner.transform
               );
    }

    private void ScheduleDestroy()
    {
        float animationDuration =
            hitboxTrack.AnimationClip.length / animator.speed;

        Destroy(gameObject, animationDuration + cleanupDelay);
    }
}
