using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class BeamWeaponAttackActor : WeaponAttackActorBase
{
    [SerializeField] private LineRenderer lineRenderer;

    private readonly HashSet<IDamageReceiver> tickTargets = new();
    private readonly HashSet<IDamageReceiver> activationHitTargets = new();

    private WeaponController controller;
    private GameObject owner;
    private WeaponData weapon;
    private WeaponLevelData level;
    private CharacterCombatSystem stats;
    private BeamWeaponAttackDefinition definition;
    private WeaponAimSnapshot snapshotAim;

    private GameObject muzzleVfxInstance;
    private GameObject impactVfxInstance;
    private float elapsed;
    private float nextDamageTick;
    private float areaMultiplier = 1f;
    private bool pulseApplied;
    private bool initialized;

    private void Reset()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    public override void Initialize(
        GameObject attackOwner,
        WeaponData weaponData,
        WeaponLevelData levelData,
        CharacterCombatSystem combatStats,
        float animationSpeed
    )
    {
        if (
            lineRenderer == null
            || attackOwner == null
            || weaponData == null || levelData?.attack is not BeamWeaponAttackDefinition beamDefinition
        )
        {
            Debug.LogError(
                "BeamWeaponAttackActor requires a LineRenderer and BeamWeaponAttackDefinition.",
                this
            );
            Destroy(gameObject);
            return;
        }

        owner = attackOwner;
        weapon = weaponData;
        level = levelData;
        stats = combatStats;
        definition = beamDefinition;
        controller = ResolveController(owner, weapon);
        snapshotAim = controller != null
            ? controller.CaptureAimSnapshot()
            : new WeaponAimSnapshot(transform.position, transform.position + transform.up, transform.up);

        areaMultiplier = ApplyAreaScale(level, stats);

        ConfigureLineRenderer();
        CreatePersistentVfx();
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        elapsed += Time.deltaTime;
        WeaponAimSnapshot aim = ResolveAim();
        float beamLength = ResolveBeamLength(aim.Origin, aim.Direction);
        Vector2 endPoint = aim.Origin + aim.Direction * beamLength;
        UpdateVisual(aim.Origin, endPoint);

        if (definition.beamMode == BeamMode.Pulse)
        {
            if (!pulseApplied)
            {
                pulseApplied = true;
                ApplyDamageTick(aim.Origin, aim.Direction, beamLength);
            }
        }
        else if (elapsed >= nextDamageTick)
        {
            nextDamageTick = elapsed + definition.damageTickInterval;
            ApplyDamageTick(aim.Origin, aim.Direction, beamLength);
        }

        if (elapsed >= definition.duration)
        {
            Destroy(gameObject);
        }
    }

    private WeaponAimSnapshot ResolveAim()
    {
        Vector2 origin = transform.position;
        if (definition.aimTracking == BeamAimTracking.FollowCursor && controller != null)
        {
            WeaponAimSnapshot currentAim = controller.CaptureAimSnapshot();
            return new WeaponAimSnapshot(origin, currentAim.Point, currentAim.Direction);
        }

        return new WeaponAimSnapshot(
            origin,
            origin + snapshotAim.Direction * definition.maximumRange,
            snapshotAim.Direction
        );
    }

    private float ResolveBeamLength(Vector2 origin, Vector2 direction)
    {
        if (definition.obstacleLayerMask.value == 0)
        {
            return definition.maximumRange;
        }

        RaycastHit2D obstacleHit = Physics2D.Raycast(
            origin,
            direction,
            definition.maximumRange,
            definition.obstacleLayerMask
        );
        return obstacleHit.collider != null ? obstacleHit.distance : definition.maximumRange;
    }

    private void ApplyDamageTick(Vector2 origin, Vector2 direction, float beamLength)
    {
        float scaledBeamWidth = definition.beamWidth * areaMultiplier;
        RaycastHit2D[] hits = scaledBeamWidth > 0f
            ? Physics2D.CircleCastAll(origin, scaledBeamWidth * 0.5f, direction, beamLength, weapon.targetLayerMask)
            : Physics2D.RaycastAll(origin, direction, beamLength, weapon.targetLayerMask);

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        tickTargets.Clear();
        int acceptedTargets = 0;

        foreach (RaycastHit2D hit in hits)
        {
            if (
                hit.collider == null
                || !DamageReceiverResolver.TryResolve(hit.collider, out IDamageReceiver target)
                || !target.IsAlive || IsOwner(target) || !tickTargets.Add(target)
            )
            {
                continue;
            }

            Vector2 hitPosition = hit.point != Vector2.zero ? hit.point : (Vector2)hit.collider.bounds.center;
            DamagePacket damage = WeaponDamageCalculator.Calculate(
                owner,
                weapon,
                level,
                stats,
                hitPosition,
                DamageType.Beam,
                definition.damageMultiplierPerTick
            );

            DamageReceipt receipt = target.ReceiveDamage(damage);
            if (receipt.Accepted)
            {
                bool firstHitThisActivation = activationHitTargets.Add(target);
                WeaponProcFlags procFlags =
                    definition.onHitPolicy == BeamOnHitPolicy.EveryDamageTick
                    || firstHitThisActivation ? WeaponProcFlags.PrimaryHit : WeaponProcFlags.CanTriggerOnKill;

                ReportHit(
                    new WeaponHitResult(
                        target,
                        target is Component component ? component.gameObject : hit.collider.gameObject,
                        weapon,
                        level,
                        damage,
                        hitPosition,
                        direction,
                        receipt,
                        WeaponEffectOrigin.PrimaryAttack,
                        procFlags,
                        chainDepth: 0
                    )
                );
                acceptedTargets++;
            }

            if (definition.hitMode == BeamHitMode.FirstTarget)
            {
                break;
            }

            if (definition.hitMode == BeamHitMode.LimitedTargets && acceptedTargets >= definition.maximumTargets)
            {
                break;
            }
        }
    }

    private void ConfigureLineRenderer()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = definition.startWidth * areaMultiplier;
        lineRenderer.endWidth = definition.endWidth * areaMultiplier;
        lineRenderer.startColor = definition.startColor;
        lineRenderer.endColor = definition.endColor;
        if (definition.beamMaterial != null)
        {
            lineRenderer.sharedMaterial = definition.beamMaterial;
        }
        lineRenderer.enabled = true;
    }

    private void CreatePersistentVfx()
    {
        if (definition.muzzleVfxPrefab != null)
        {
            muzzleVfxInstance = Instantiate(
                definition.muzzleVfxPrefab,
                transform.position,
                transform.rotation,
                transform
            );
        }

        if (definition.impactVfxPrefab != null)
        {
            impactVfxInstance = Instantiate(definition.impactVfxPrefab, transform.position, Quaternion.identity);
            impactVfxInstance.transform.localScale *= areaMultiplier;
        }
    }

    private void UpdateVisual(Vector2 origin, Vector2 endPoint)
    {
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, endPoint);

        if (impactVfxInstance != null)
        {
            impactVfxInstance.transform.position = endPoint;
        }
    }

    private bool IsOwner(IDamageReceiver target)
    {
        return owner != null && target is Component component && component.transform.root == owner.transform.root;
    }

    private static WeaponController ResolveController(GameObject owner, WeaponData weapon)
    {
        WeaponController[] controllers = owner.GetComponents<WeaponController>();
        foreach (WeaponController weaponController in controllers)
        {
            if (weaponController.Data == weapon)
            {
                return weaponController;
            }
        }

        return controllers.Length > 0 ? controllers[0] : null;
    }

    private void OnDestroy()
    {
        if (impactVfxInstance != null)
        {
            Destroy(impactVfxInstance);
        }
    }
}
