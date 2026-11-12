using System.Collections;
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
    private WeaponEvolutionData activeEvolution;
    private int activeEvolutionIndex = -1;
    private WeaponData ownershipData;
    private bool attacksSuspended;
    private IWeaponAimProvider2D aimProvider;
    private bool configurationReady;

    private readonly object critStackStatKey = new();
    private readonly List<CombatStatBonus> critStackBonuses = new();
    private int critStacks;
    private float critStackTimer;

    public int CritStacks => critStacks;
    public float CritStackTimeRemaining => critStackTimer;
    public bool HasActiveCritStack => ActiveCritStack != null;

    private WeaponEvolutionCritStackData ActiveCritStack =>
        IsEvolved && weaponData?.evolutionCritStack != null && weaponData.evolutionCritStack.enabled
            ? weaponData.evolutionCritStack
            : null;

    public int CurrentLevelIndex => currentLevelIndex;
    public float RemainingCooldown => remainingCooldown;
    public WeaponData Data => weaponData;

    /// <summary>The weapon the level-up draft knows this controller by, even after a form swap.</summary>
    public WeaponData OwnershipData => ownershipData != null ? ownershipData : weaponData;

    /// <summary>Stops all attacks while keeping level and stat bonuses, for a form that has no weapon yet.</summary>
    public bool AttacksSuspended
    {
        get => attacksSuspended;
        set
        {
            if (attacksSuspended == value)
            {
                return;
            }

            attacksSuspended = value;
            if (attacksSuspended)
            {
                DestroyPersistentActor();
            }
            else if (configurationReady && isActiveAndEnabled)
            {
                EnsurePersistentActor();
            }
        }
    }

    /// <summary>True when a character skill drives this weapon: it takes no slot and is never offered in drafts.</summary>
    public bool IsSkillWeapon { get; set; }

    public WeaponLevelData CurrentLevel =>
        activeEvolution != null ? activeEvolution : weaponData?.GetLevel(currentLevelIndex);
    public int DisplayLevel => IsEvolved ? MaximumLevel : currentLevelIndex + 1;
    public int MaximumLevel => NormalLevelCount + (HasConfiguredEvolution() ? 1 : 0);
    public bool IsMaxLevel => activeEvolutionIndex >= 0 || (IsAtLastNormalLevel && !HasConfiguredEvolution());
    public WeaponEvolutionData ActiveEvolution => activeEvolution;
    public bool IsEvolved => activeEvolution != null;
    public bool CanEvolve =>
        configurationReady && activeEvolutionIndex < 0 && IsAtLastNormalLevel && HasConfiguredEvolution();

    private int NormalLevelCount => weaponData?.levels?.Count ?? 0;
    private bool IsAtLastNormalLevel => currentLevelIndex >= NormalLevelCount - 1;

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
        ClearCritStacks();
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
        TickCritStacks();

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

        if (level.attack.attackRepeatCount > 1)
        {
            StartCoroutine(RepeatAttack(level));
        }

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

    public bool Evolve(WeaponEvolutionData evolution)
    {
        if (!CanEvolve || !weaponData.IsFormConfigured(evolution) || !weaponData.evolutions.Contains(evolution))
        {
            return false;
        }

        activeEvolutionIndex = weaponData.evolutions.IndexOf(evolution);
        activeEvolution = evolution.CreateRuntimeForm(weaponData.LastNormalLevel);
        RefreshActorAfterLevelChange();
        return true;
    }

    /// <summary>
    /// Replaces the weapon but keeps the level and the chosen evolution, so a character's forms share progress.
    /// The new weapon uses its evolution at the same position in its list.
    /// </summary>
    public bool SwapWeaponData(WeaponData data)
    {
        if (data == null || data.levels == null || data.levels.Count == 0)
        {
            return false;
        }

        if (data == weaponData)
        {
            return true;
        }

        ownershipData = OwnershipData;
        DestroyPersistentActor();
        ClearCritStacks();
        weaponData = data;
        nextEffectTimes.Clear();
        activeEvolution = CreateEvolutionAt(activeEvolutionIndex);
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
        ClearCritStacks();
        combatStats?.RemoveStatProvider(this);
        weaponData = data;
        combatStats = ownerCombatStats;
        attackOrigin = runtimeAttackOrigin != null ? runtimeAttackOrigin : transform;
        aimProviderBehaviour = runtimeAimProvider;
        currentLevelIndex = 0;
        activeEvolution = null;
        activeEvolutionIndex = -1;
        ownershipData = null;
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
        if (isActiveAndEnabled)
        {
            EnsurePersistentActor();
        }

        return true;
    }

    public void SetLevel(int levelIndex)
    {
        if (weaponData?.levels == null || weaponData.levels.Count == 0)
        {
            return;
        }

        int newLevelIndex = Mathf.Clamp(levelIndex, 0, weaponData.levels.Count - 1);
        if (newLevelIndex != currentLevelIndex || IsEvolved)
        {
            currentLevelIndex = newLevelIndex;
            activeEvolution = null;
            activeEvolutionIndex = -1;
            ClearCritStacks();
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
        if (persistentActor == null || !autoAttack || attacksSuspended || remainingCooldown > 0f)
        {
            return;
        }

        TriggerEffects(WeaponEffectTrigger.OnAttack, CreateAttackContext());
        persistentActor.ExecuteAttack();
        remainingCooldown = CalculateCurrentInterval();
    }

    private IEnumerator RepeatAttack(WeaponLevelData level)
    {
        WaitForSeconds delay = new(level.attack.attackRepeatDelay / CalculateAttackSpeedMultiplier());
        for (int i = 1; i < level.attack.attackRepeatCount; i++)
        {
            yield return delay;

            // A level-up or form switch during the wait must not fire the old attack.
            if (CurrentLevel != level || attacksSuspended)
            {
                yield break;
            }

            SpawnAttackActor(level);
            TriggerEffects(WeaponEffectTrigger.OnAttack, CreateAttackContext());
        }
    }

    private bool CanAttack()
    {
        return configurationReady
            && isActiveAndEnabled
            && !attacksSuspended && remainingCooldown <= 0f && CurrentLevel?.attack?.attackPrefab != null;
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
        if (
            attacksSuspended
            || !UsesPersistentActor || persistentActor != null || CurrentLevel?.attack?.attackPrefab == null
        )
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

    public void HandleHitResolved(WeaponHitResult hit)
    {
        combatStats?.ReportDamageDealt(hit.Receipt.AppliedAmount);

        if (hit.KilledTarget)
        {
            AddCritStack();
        }

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

            WeaponAreaUtility.ScaleNewInstance(effectObject.transform, context.Weapon, context.Stats);

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
            CaptureAimSnapshot().Direction,
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
        if (weaponData == null)
        {
            return 1f;
        }

        float interval = weaponData.attackInterval;
        if (weaponData.affectedByHaste && combatStats != null)
        {
            float haste = combatStats.GetStat(CombatStatType.AttackSpeedPercent);
            float hasteMultiplier = 1f + haste / 100f;
            interval /= Mathf.Max(0.01f, hasteMultiplier);
        }

        if (weaponData.minimumAttackInterval >= 0f)
        {
            interval = Mathf.Max(weaponData.minimumAttackInterval, interval);
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
        return WeaponAreaUtility.ResolveMultiplier(weaponData, combatStats);
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

        if (activeEvolution != null)
        {
            cumulativeLevelBonuses.AddRange(activeEvolution.statBonuses);
            cumulativeLevelRestrictions.AddRange(activeEvolution.statRestrictions);
        }

        WeaponEvolutionCritStackData critStack = ActiveCritStack;
        if (critStack != null && critStack.convertOtherCritBonusesToAtk)
        {
            cumulativeLevelRestrictions.Add(
                new CombatStatRestriction
                {
                    stat = CombatStatType.CriticalChancePercent,
                    affectedSources =
                        CombatStatSource.All & ~CombatStatSource.BaseStats & ~CombatStatSource.EvolutionPassive,
                    mode = StatRestrictionMode.Convert,
                    convertedStat = CombatStatType.AttackPowerPercent,
                    conversionRatio = 1f / critStack.excessCritPerAtkPercent,
                    allowOwningSource = false,
                }
            );
        }

        combatStats.SetStatBonuses(this, CombatStatSource.WeaponLevel, cumulativeLevelBonuses);
        combatStats.SetStatRestrictions(this, cumulativeLevelRestrictions);
    }

    private void AddCritStack()
    {
        WeaponEvolutionCritStackData critStack = ActiveCritStack;
        if (critStack == null || combatStats == null)
        {
            return;
        }

        critStacks++;
        critStackTimer = critStack.stackDuration;

        // Measured without this passive so the cap only sees Critical Chance from everything else.
        combatStats.RemoveStatProvider(critStackStatKey);
        float otherCrit = combatStats.GetStat(CombatStatType.CriticalChancePercent);
        float stackCrit = critStacks * critStack.critPerKill;
        float appliedCrit = Mathf.Clamp(critStack.critCap - otherCrit, 0f, stackCrit);
        float excessCrit = stackCrit - appliedCrit;

        critStackBonuses.Clear();
        critStackBonuses.Add(new CombatStatBonus(CombatStatType.CriticalChancePercent, appliedCrit));
        critStackBonuses.Add(
            new CombatStatBonus(CombatStatType.AttackPowerPercent, excessCrit / critStack.excessCritPerAtkPercent)
        );
        combatStats.SetStatBonuses(critStackStatKey, this, CombatStatSource.EvolutionPassive, critStackBonuses);
    }

    private void TickCritStacks()
    {
        if (critStacks <= 0)
        {
            return;
        }

        critStackTimer -= Time.deltaTime;
        if (critStackTimer <= 0f)
        {
            ClearCritStacks();
        }
    }

    private void ClearCritStacks()
    {
        critStacks = 0;
        critStackTimer = 0f;
        combatStats?.RemoveStatProvider(critStackStatKey);
    }

    private WeaponEvolutionData CreateEvolutionAt(int index)
    {
        if (index < 0 || weaponData.evolutions == null || index >= weaponData.evolutions.Count)
        {
            return null;
        }

        WeaponEvolutionData evolution = weaponData.evolutions[index];
        return weaponData.IsFormConfigured(evolution) ? evolution.CreateRuntimeForm(weaponData.LastNormalLevel) : null;
    }

    private bool HasConfiguredEvolution()
    {
        if (weaponData?.evolutions == null)
        {
            return false;
        }

        foreach (WeaponEvolutionData evolution in weaponData.evolutions)
        {
            if (weaponData.IsFormConfigured(evolution))
            {
                return true;
            }
        }

        return false;
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
