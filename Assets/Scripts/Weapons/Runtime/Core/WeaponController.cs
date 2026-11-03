using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class WeaponController : MonoBehaviour
{
    private const int MaximumEffectChainDepth = 4;

    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField, Min(0)] private int startingLevel;

    [Header("Owner")]
    [SerializeField] private CharacterCombatSystem combatStats;
    [SerializeField] private Transform attackOrigin;

    [SerializeField]
    [Tooltip("Optional MonoBehaviour implementing IWeaponAimProvider2D. LookAtMouse is supported.")]
    private MonoBehaviour aimProviderBehaviour;

    [Header("Behaviour")]
    [SerializeField] private bool autoAttack = true;

    [SerializeField]
    [FormerlySerializedAs("attackImediately")]
    private bool attackImmediately = true;

    [Header("Runtime Debug")]
    [SerializeField] private int currentLevelIndex;
    [SerializeField] private float remainingCooldown;

    private readonly Dictionary<WeaponTriggeredEffectData, float> nextEffectTimes = new();
    private readonly List<CombatStatBonus> cumulativeLevelBonuses = new();
    private readonly List<CombatStatRestriction> cumulativeLevelRestrictions = new();

    private WeaponAttackActorBase persistentActor;
    private WeaponAttackActorBase persistentActorPrefab;
    private IWeaponAimProvider2D aimProvider;
    private bool configurationReady;

    public int CurrentLevelIndex => currentLevelIndex;
    public float RemainingCooldown => remainingCooldown;
    public WeaponData Data => weaponData;
    public WeaponLevelData CurrentLevel => weaponData?.GetLevel(currentLevelIndex);
    public int DisplayLevel => currentLevelIndex + 1;
    public int MaximumLevel => weaponData?.levels?.Count ?? 0;
    public bool IsMaxLevel => MaximumLevel <= 0 || currentLevelIndex >= MaximumLevel - 1;

    public float GetEffectiveStat(CombatStatType stat)
    {
        if (combatStats != null && weaponData != null)
        {
            return combatStats.GetStat(stat);
        }

        return stat switch
        {
            CombatStatType.AttackPowerPercent => 100f,
            CombatStatType.MovementSpeedPercent => 100f,
            CombatStatType.WeaponAreaPercent => 100f,
            _ => 0f,
        };
    }

    public float GetOwnerStat(CombatStatType stat)
    {
        return combatStats != null ? combatStats.GetStat(stat) : 0f;
    }

    public WeaponAimSnapshot CaptureAimSnapshot()
    {
        Vector2 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        if (aimProvider != null && aimProvider.TryGetAim(origin, out Vector2 point, out Vector2 direction))
        {
            return new WeaponAimSnapshot(origin, point, direction);
        }

        Vector2 fallbackDirection = attackOrigin != null ? (Vector2)attackOrigin.up : Vector2.up;
        return new WeaponAimSnapshot(origin, origin + fallbackDirection, fallbackDirection);
    }

    private bool UsesPersistentActor =>
        CurrentLevel?.attack != null && CurrentLevel.attack.lifetime == WeaponAttackActorLifetime.Persistent;

    private void Awake()
    {
        ResolveReferences();
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        currentLevelIndex = Mathf.Clamp(startingLevel, 0, weaponData.levels.Count - 1);
        RefreshLevelStatBonuses();
        remainingCooldown = attackImmediately ? 0f : CalculateCurrentInterval();
        configurationReady = true;
    }

    private void OnEnable()
    {
        if (configurationReady)
        {
            RefreshLevelStatBonuses();
            EnsurePersistentActor();
        }
    }

    private void OnDisable()
    {
        combatStats?.RemoveStatProvider(this);
        DestroyPersistentActor();
    }

    private void Update()
    {
        if (!configurationReady)
        {
            return;
        }

        remainingCooldown = Mathf.Max(0f, remainingCooldown - Time.deltaTime);

        if (UsesPersistentActor)
        {
            UpdatePersistentAttack();
        }
        else
        {
            UpdateTransientAttack();
        }
    }

    public bool TryAttack()
    {
        if (!CanAttack() || UsesPersistentActor)
        {
            return false;
        }

        WeaponLevelData level = CurrentLevel;
        SpawnAttackActor(level);
        TriggerEffects(WeaponEffectTrigger.OnAttack, CreateAttackContext());
        remainingCooldown = CalculateAttackInterval();
        return true;
    }

    public bool LevelUp()
    {
        if (weaponData?.levels == null || weaponData.levels.Count == 0)
        {
            return false;
        }

        int maximumLevelIndex = weaponData.levels.Count - 1;
        if (currentLevelIndex >= maximumLevelIndex)
        {
            return false;
        }

        currentLevelIndex++;
        RefreshActorAfterLevelChange();
        return true;
    }

    public bool ConfigureRuntime(
        WeaponData data,
        CharacterCombatSystem ownerCombatStats,
        Transform runtimeAttackOrigin = null,
        MonoBehaviour runtimeAimProvider = null
    )
    {
        if (data == null || data.levels == null || data.levels.Count == 0)
        {
            Debug.LogError("Cannot configure a runtime weapon without valid WeaponData levels.", this);
            return false;
        }

        configurationReady = false;
        DestroyPersistentActor();
        combatStats?.RemoveStatProvider(this);
        weaponData = data;
        combatStats = ownerCombatStats;
        attackOrigin = runtimeAttackOrigin != null ? runtimeAttackOrigin : transform;
        aimProviderBehaviour = runtimeAimProvider;
        currentLevelIndex = 0;
        nextEffectTimes.Clear();

        ResolveReferences();
        if (!ValidateConfiguration())
        {
            enabled = false;
            return false;
        }

        enabled = true;
        configurationReady = true;
        RefreshLevelStatBonuses();
        remainingCooldown = attackImmediately ? 0f : CalculateCurrentInterval();
        EnsurePersistentActor();
        return true;
    }

    public void SetLevel(int levelIndex)
    {
        if (weaponData?.levels == null || weaponData.levels.Count == 0)
        {
            return;
        }

        int newLevelIndex = Mathf.Clamp(levelIndex, 0, weaponData.levels.Count - 1);
        if (newLevelIndex != currentLevelIndex)
        {
            currentLevelIndex = newLevelIndex;
            RefreshActorAfterLevelChange();
        }
    }

    public void ResetCooldown()
    {
        remainingCooldown = 0f;
    }

    public DamageReceipt ApplyEffectDamage(
        in WeaponEffectContext sourceContext,
        IDamageReceiver target,
        GameObject targetObject,
        in DamagePacket damage,
        WeaponEffectOrigin origin,
        bool canTriggerOnHit
    )
    {
        if (target == null || !target.IsAlive || sourceContext.ChainDepth > MaximumEffectChainDepth)
        {
            return DamageReceipt.Rejected;
        }

        DamageReceipt receipt = target.ReceiveDamage(damage);
        if (!receipt.Accepted)
        {
            return receipt;
        }

        WeaponProcFlags procFlags = WeaponProcFlags.Secondary | WeaponProcFlags.CanTriggerOnKill;
        if (canTriggerOnHit)
        {
            procFlags |= WeaponProcFlags.CanTriggerOnHit;
        }

        HandleHitResolved(
            new WeaponHitResult(
                target,
                targetObject,
                sourceContext.Weapon,
                sourceContext.Level,
                damage,
                damage.HitPosition,
                sourceContext.Direction,
                receipt,
                origin,
                procFlags,
                sourceContext.ChainDepth
            )
        );

        return receipt;
    }

    [ContextMenu("Debug/Level Up Weapon")]
    public void DebugLevelUp()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int previousLevelIndex = currentLevelIndex;
        if (LevelUp())
        {
            Debug.Log(
                $"[WEAPON DEBUG] {weaponData.displayName}: "
                    + $"Level {previousLevelIndex + 1} -> {currentLevelIndex + 1}",
                this
            );
            return;
        }

        Debug.LogWarning(
            $"[WEAPON DEBUG] Không thể tăng level cho {(weaponData != null ? weaponData.displayName : name)}. "
                + "Vũ khí có thể đã đạt level tối đa hoặc chưa được cấu hình.",
            this
        );
#endif
    }

    private void UpdateTransientAttack()
    {
        if (autoAttack)
        {
            TryAttack();
        }
    }

    private void UpdatePersistentAttack()
    {
        EnsurePersistentActor();
        if (persistentActor == null || !autoAttack || remainingCooldown > 0f)
        {
            return;
        }

        TriggerEffects(WeaponEffectTrigger.OnAttack, CreateAttackContext());
        persistentActor.ExecuteAttack();
        remainingCooldown = CalculateCurrentInterval();
    }

    private bool CanAttack()
    {
        return configurationReady
            && isActiveAndEnabled && remainingCooldown <= 0f && CurrentLevel?.attack?.attackPrefab != null;
    }

    private WeaponAttackActorBase SpawnAttackActor(WeaponLevelData level)
    {
        WeaponAttackActorBase actor = Instantiate(level.attack.attackPrefab, attackOrigin);
        actor.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        actor.HitResolved += HandleHitResolved;

        float animationSpeed = UsesPersistentActor ? 1f : CalculateAttackSpeedMultiplier();
        actor.Initialize(gameObject, weaponData, level, combatStats, animationSpeed);
        return actor;
    }

    private void EnsurePersistentActor()
    {
        if (!UsesPersistentActor || persistentActor != null || CurrentLevel?.attack?.attackPrefab == null)
        {
            return;
        }

        persistentActor = SpawnAttackActor(CurrentLevel);
        persistentActorPrefab = CurrentLevel.attack.attackPrefab;
    }

    private void DestroyPersistentActor()
    {
        if (persistentActor == null)
        {
            return;
        }

        persistentActor.HitResolved -= HandleHitResolved;
        persistentActor.gameObject.SetActive(false);
        Destroy(persistentActor.gameObject);

        persistentActor = null;
        persistentActorPrefab = null;
    }

    private void RefreshActorAfterLevelChange()
    {
        RefreshLevelStatBonuses();

        if (!configurationReady)
        {
            return;
        }

        if (UsesPersistentActor && persistentActor != null && persistentActorPrefab == CurrentLevel.attack.attackPrefab)
        {
            persistentActor.RefreshLevel(CurrentLevel);
            remainingCooldown = Mathf.Min(remainingCooldown, CalculateCurrentInterval());
            return;
        }

        DestroyPersistentActor();
        remainingCooldown = attackImmediately ? 0f : CalculateCurrentInterval();

        if (UsesPersistentActor)
        {
            EnsurePersistentActor();
        }
    }

    private void HandleHitResolved(WeaponHitResult hit)
    {
        WeaponData hitWeapon = hit.Weapon != null ? hit.Weapon : weaponData;
        WeaponLevelData hitLevel = hit.Level ?? CurrentLevel;
        Vector2 hitDirection = hit.Direction.sqrMagnitude > 0f ? hit.Direction.normalized : (Vector2)attackOrigin.up;

        WeaponEffectContext context = new WeaponEffectContext(
            this,
            gameObject,
            hitWeapon,
            hitLevel,
            combatStats,
            hit.Target,
            hit.TargetObject,
            hit.HitPosition,
            hitDirection,
            hit.Damage,
            hit.Receipt,
            hit.Origin,
            hit.ProcFlags,
            hit.ChainDepth
        );

        if ((hit.ProcFlags & WeaponProcFlags.CanTriggerOnHit) != 0)
        {
            TriggerEffects(WeaponEffectTrigger.OnHit, context);
        }

        if (hit.KilledTarget && (hit.ProcFlags & WeaponProcFlags.CanTriggerOnKill) != 0)
        {
            TriggerEffects(WeaponEffectTrigger.OnKill, context);
        }
    }

    private void TriggerEffects(WeaponEffectTrigger trigger, in WeaponEffectContext context)
    {
        TriggerEffects(context.Level?.triggeredEffects, trigger, context);
    }

    private void TriggerEffects(
        IReadOnlyList<WeaponTriggeredEffectData> effects,
        WeaponEffectTrigger trigger,
        in WeaponEffectContext context
    )
    {
        if (effects == null || context.ChainDepth >= MaximumEffectChainDepth)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            WeaponTriggeredEffectData effectData = effects[i];
            if (!CanTriggerEffect(effectData, trigger, context))
            {
                continue;
            }

            if (effectData.internalCooldown > 0f)
            {
                nextEffectTimes[effectData] = Time.time + effectData.internalCooldown;
            }

            WeaponEffectContext childContext = context.CreateChild(
                effectData.effect != null ? WeaponEffectOrigin.DirectEffect : WeaponEffectOrigin.TriggeredActor,
                effectData.generatedHitsCanTriggerOnHit
            );

            if (effectData.effect != null)
            {
                effectData.effect.Apply(childContext);
                continue;
            }

            GameObject effectObject = Instantiate(effectData.effectPrefab, context.Position, Quaternion.identity);

            WeaponAreaUtility.ScaleNewInstance(effectObject.transform, context.Level, context.Stats);

            if (!effectObject.TryGetComponent(out WeaponTriggeredEffectActor instance))
            {
                Debug.LogError(
                    $"Triggered Effect prefab '{effectData.effectPrefab.name}' must have a "
                        + $"{nameof(WeaponTriggeredEffectActor)} component on its root GameObject.",
                    effectObject
                );
                Destroy(effectObject);
                continue;
            }

            instance.HitResolved += HandleHitResolved;
            instance.Initialize(childContext, effectData);
        }
    }

    private bool CanTriggerEffect(
        WeaponTriggeredEffectData effectData,
        WeaponEffectTrigger trigger,
        in WeaponEffectContext context
    )
    {
        if (
            effectData == null
            || effectData.trigger != trigger
            || (effectData.effect == null && effectData.effectPrefab == null)
            || (context.IsSecondary && !effectData.allowSecondaryHits)
        )
        {
            return false;
        }

        if (nextEffectTimes.TryGetValue(effectData, out float nextAllowedTime) && Time.time < nextAllowedTime)
        {
            return false;
        }

        float triggerChance = Mathf.Clamp01(effectData.triggerChance);
        return triggerChance >= 1f || (triggerChance > 0f && Random.value <= triggerChance);
    }

    private WeaponEffectContext CreateAttackContext()
    {
        return new WeaponEffectContext(
            this,
            gameObject,
            weaponData,
            CurrentLevel,
            combatStats,
            null,
            null,
            attackOrigin.position,
            attackOrigin.up,
            default,
            default,
            WeaponEffectOrigin.PrimaryAttack,
            WeaponProcFlags.PrimaryHit,
            chainDepth: 0
        );
    }

    private float CalculateCurrentInterval()
    {
        if (UsesPersistentActor)
        {
            return Mathf.Max(0.01f, CurrentLevel.attack.hitCooldown);
        }

        return CalculateAttackInterval();
    }

    private float CalculateAttackInterval()
    {
        WeaponLevelData level = CurrentLevel;
        if (level == null)
        {
            return 1f;
        }

        float interval = level.attackInterval;
        if (weaponData.affectedByHaste && combatStats != null)
        {
            float haste = combatStats.GetStat(CombatStatType.AttackSpeedPercent);
            float hasteMultiplier = 1f + haste / 100f;
            interval /= Mathf.Max(0.01f, hasteMultiplier);
        }

        if (level.minimumAttackInterval >= 0f)
        {
            interval = Mathf.Max(level.minimumAttackInterval, interval);
        }

        return Mathf.Max(0.01f, interval);
    }

    private float CalculateAttackSpeedMultiplier()
    {
        if (!weaponData.affectedByHaste || combatStats == null)
        {
            return 1f;
        }

        float haste = combatStats.GetStat(CombatStatType.AttackSpeedPercent);
        return Mathf.Max(0.01f, 1f + haste / 100f);
    }

    public float CalculateCurrentAreaMultiplier()
    {
        return WeaponAreaUtility.ResolveMultiplier(CurrentLevel, combatStats);
    }

    private void RefreshLevelStatBonuses()
    {
        if (combatStats == null || weaponData?.levels == null || weaponData.levels.Count == 0)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            combatStats.RemoveStatProvider(this);
            return;
        }

        cumulativeLevelBonuses.Clear();
        cumulativeLevelRestrictions.Clear();
        int lastLevel = Mathf.Clamp(currentLevelIndex, 0, weaponData.levels.Count - 1);
        for (int levelIndex = 0; levelIndex <= lastLevel; levelIndex++)
        {
            List<CombatStatBonus> levelBonuses = weaponData.levels[levelIndex]?.statBonuses;
            if (levelBonuses != null)
            {
                cumulativeLevelBonuses.AddRange(levelBonuses);
            }

            List<CombatStatRestriction> levelRestrictions = weaponData.levels[levelIndex]?.statRestrictions;
            if (levelRestrictions != null)
            {
                cumulativeLevelRestrictions.AddRange(levelRestrictions);
            }
        }

        combatStats.SetStatBonuses(this, CombatStatSource.WeaponLevel, cumulativeLevelBonuses);
        combatStats.SetStatRestrictions(this, cumulativeLevelRestrictions);
    }

    private void ResolveReferences()
    {
        if (combatStats == null)
        {
            combatStats = GetComponentInParent<CharacterCombatSystem>(true);
        }

        if (attackOrigin == null)
        {
            attackOrigin = transform;
        }

        aimProvider = aimProviderBehaviour as IWeaponAimProvider2D;
        if (aimProvider == null)
        {
            MonoBehaviour[] candidates = GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour candidate in candidates)
            {
                if (candidate is IWeaponAimProvider2D found)
                {
                    aimProvider = found;
                    aimProviderBehaviour = candidate;
                    break;
                }
            }
        }
    }

    private bool ValidateConfiguration()
    {
        if (weaponData == null)
        {
            Debug.LogError("WeaponController chưa được gán WeaponData.", this);
            return false;
        }

        if (weaponData.levels == null || weaponData.levels.Count == 0)
        {
            Debug.LogError($"Weapon {weaponData.name} chưa có level.", this);
            return false;
        }

        return true;
    }
}
