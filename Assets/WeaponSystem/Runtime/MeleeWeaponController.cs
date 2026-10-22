using System;
using UnityEngine;
using Random = UnityEngine.Random;
[DisallowMultipleComponent]
public sealed class MeleeWeaponController : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField]
    private MeleeWeaponData weaponData;
    private MeleeAttackActorBase persistentActor;
    private MeleeAttackActorBase persistentActorPrefab;
    private bool configurationReady;

    [SerializeField, Min(0)] private int startingLevel;

    [Header("Owner")]
    [SerializeField]
    private CharacterCombatSystem combatStats;

    private Transform attackOrigin;

    [Header("Behaviour")]
    [SerializeField] private bool autoAttack = true;

    [SerializeField]
    private bool attackImediately = true;

    [Header("RuntimeDebug")]
    [SerializeField] private int currentLevelIndex;

    [SerializeField]
    private float remainingCooldown;
    public int CurrentLevelIndex => currentLevelIndex;

    [Header("Awakening")]
    [SerializeField]
    private bool isAwakened;

    public bool IsAwakened => isAwakened;

    public float RemainingCooldown =>
     remainingCooldown;

    public MeleeWeaponLevelData CurrentLevel
    {
        get
        {
            if (weaponData == null)
                return null;

            return weaponData.GetLevel(
                currentLevelIndex
            );
        }
    }

    private void Awake()
    {
        if (combatStats == null)
        {
            combatStats = GetComponent<CharacterCombatSystem>();
        }

        if (attackOrigin == null) attackOrigin = transform;

        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        currentLevelIndex = Mathf.Clamp(startingLevel, 0, weaponData.levels.Count - 1);

        remainingCooldown = attackImediately ? 0f : CalculateCurrentInterval();
        configurationReady = true;
    }

    private void Update()
    {
        if (!configurationReady) return;

        if (UsesPersistentActor)
        {
            UpdatePersistentAttack();
            return;
        }

        remainingCooldown = Mathf.Max(0f, remainingCooldown - Time.deltaTime);

        if (autoAttack)
        {
            TryAttack();
        }
    }

    public bool TryAttack()
    {
        if (!configurationReady || !isActiveAndEnabled || UsesPersistentActor)
            return false;

        if (remainingCooldown > 0f)
        {
            return false;
        }

        MeleeWeaponLevelData level = CurrentLevel;

        if (level == null || level.attack == null || level.attack.attackPrefab == null) return false;

        SpawnAttack(level);

        TriggerOnAttackEffects();

        remainingCooldown = CalculateAttackInterval();

        return true;
    }

    private MeleeAttackActorBase SpawnAttack(MeleeWeaponLevelData level)
    {
        MeleeAttackActorBase actor = Instantiate(level.attack.attackPrefab, attackOrigin);
        actor.transform.localPosition = Vector3.zero;
        actor.transform.localRotation = Quaternion.identity;

        actor.HitResolved += HandleHitResolved;

        float animationSpeed = UsesPersistentActor ? 1f : CalculateAttackSpeedMultiplier();
        actor.Initialize(gameObject, weaponData, level, combatStats, animationSpeed);

        return actor;
    }

    private float CalculateAttackInterval()
    {
        MeleeWeaponLevelData level = CurrentLevel;

        if (level == null) return 1f;

        float interval = level.attackInterval;

        if (weaponData.affectByHaste && combatStats != null)
        {
            float hatseMutiplier = 1f + combatStats.HastePercent / 100f;

            interval /= hatseMutiplier;
        }

        if (level.minimumAttackInterval >= 0f)
        {
            interval = Mathf.Max(level.minimumAttackInterval, interval);
        }

        return Mathf.Max(0.01f, interval);
    }

    private float CalculateCurrentInterval()
    {
        // Aura uses a fixed damage interval, independent of Haste.
        return UsesPersistentActor
            ? Mathf.Max(0.01f, CurrentLevel.attack.hitCooldown)
            : CalculateAttackInterval();
    }

    public bool LevelUp()
    {
        if (weaponData == null || weaponData.levels == null) return false;

        int maximunLevellIndex = weaponData.levels.Count - 1;
        if (currentLevelIndex >= maximunLevellIndex)
        {
            TryAwaken();
            return false;
        }

        currentLevelIndex++;
        RefreshActorAfterLevelChange();

        TryAwaken();
        return true;
    }

    [ContextMenu("Debug/Level Up Weapon")]
    public void DebugLevelUp()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int previousLevelIndex = currentLevelIndex;

        if (LevelUp())
        {
            Debug.Log(
                $"[WEAPON DEBUG] {weaponData.displayName}: " +
                $"Level {previousLevelIndex + 1} -> {currentLevelIndex + 1}",
                this
            );

            return;
        }

        Debug.LogWarning(
            $"[WEAPON DEBUG] Không thể tăng level cho " +
            $"{(weaponData != null ? weaponData.displayName : name)}. " +
            "Vũ khí có thể đã đạt level tối đa hoặc chưa được cấu hình.",
            this
        );
#endif
    }


    public void SetLevel(int levelIndex)
    {
        if (weaponData == null ||
            weaponData.levels == null ||
            weaponData.levels.Count == 0)
        {
            return;
        }

        int newLevelIndex = Mathf.Clamp(
            levelIndex,
            0,
            weaponData.levels.Count - 1
        );
        if (newLevelIndex != currentLevelIndex)
        {
            currentLevelIndex = newLevelIndex;
            RefreshActorAfterLevelChange();
        }
        TryAwaken();
    }

    public void ResetCooldown()
    {
        remainingCooldown = 0f;
    }

    private bool ValidateConfiguration()
    {
        if (weaponData == null)
        {
            Debug.LogError(
                "MeleeWeaponController chưa được " +
                "gán MeleeWeaponData.",
                this
            );

            return false;
        }

        if (weaponData.levels == null ||
            weaponData.levels.Count == 0)
        {
            Debug.LogError(
                $"Weapon {weaponData.name} " +
                "chưa có level.",
                this
            );

            return false;
        }

        return true;
    }

    private float CalculateAttackSpeedMultiplier()
    {
        if (!weaponData.affectByHaste || combatStats == null)
            return 1f;

        return Mathf.Max(
            0.01f,
            1f + combatStats.HastePercent / 100f
        );
    }
    private bool UsesPersistentActor
    {
        get
        {
            MeleeWeaponLevelData level = CurrentLevel;

            return level != null &&
                   level.attack != null &&
                   level.attack.lifeTime ==
                       MeleeAttackActorLifeTime.Persistent;
        }
    }

    private void OnEnable()
    {
        if (!configurationReady) return;

        EnsurePersistentActor();
    }

    private void EnsurePersistentActor()
    {
        if (!UsesPersistentActor || persistentActor != null) return;

        MeleeWeaponLevelData level = CurrentLevel;
        if (level == null || level.attack == null || level.attack.attackPrefab == null) return;

        persistentActor = SpawnAttack(level);
        persistentActorPrefab = level.attack.attackPrefab;
    }

    private void DestroyPersistentActor()
    {
        if (persistentActor == null)
            return;

        persistentActor.gameObject.SetActive(false);
        persistentActor.HitResolved -= HandleHitResolved;
        Destroy(persistentActor.gameObject);
        persistentActor = null;
        persistentActorPrefab = null;
    }

    private void RefreshActorAfterLevelChange()
    {
        if (!configurationReady) return;

        if (UsesPersistentActor && persistentActor != null &&
            persistentActorPrefab == CurrentLevel.attack.attackPrefab)
        {
            persistentActor.RefreshLevel(CurrentLevel);
            remainingCooldown = Mathf.Min(remainingCooldown, CalculateCurrentInterval());
            return;
        }

        // Replace only when an upgrade changes the prefab or lifetime.
        DestroyPersistentActor();
        remainingCooldown = attackImediately ? 0f : CalculateCurrentInterval();
        if (UsesPersistentActor) EnsurePersistentActor();
    }
    private void UpdatePersistentAttack()
    {
        EnsurePersistentActor();

        if (persistentActor == null)
            return;

        remainingCooldown = Mathf.Max(0f, remainingCooldown - Time.deltaTime);

        if (!autoAttack || remainingCooldown > 0f)
            return;

        persistentActor.ExecuteAttack();

        // Không tính Haste.
        remainingCooldown = CalculateCurrentInterval();
    }

    public bool TryAwaken()
    {
        if (isAwakened) return false;

        if (weaponData == null || weaponData.awkening == null) return false;

        if (currentLevelIndex < weaponData.awkening.requiredLevelIndex - 1) return false;

        isAwakened = true;
        return true;
    }

    private void TriggerOnAttackEffects()
    {
        WeaponEffectContext context = new WeaponEffectContext(
            gameObject,
            weaponData,
            CurrentLevel,
            combatStats,
            null,
            attackOrigin.position,
            attackOrigin.up,
            default
        );

        TryTriggerAwakenEffect(
            WeaponEffectTrigger.OnAttack,
            context
        );
    }

    private void HandleHitResolved(MeleeHitResult hit)
    {
        if (!hit.KilledTarget) return;

        WeaponEffectContext context = new WeaponEffectContext(
            gameObject,
            weaponData,
            CurrentLevel,
            combatStats,
            hit.Target,
            hit.HitPosition,
            attackOrigin.up,
            hit.Damage
        );

        
        TryTriggerAwakenEffect(
            WeaponEffectTrigger.OnKill,
            context
        );
    }

    private void TryTriggerAwakenEffect(WeaponEffectTrigger trigger, WeaponEffectContext context)
    {
        if (!isAwakened) return;

        if (weaponData == null || weaponData.awkening == null) return;

        WeaponTriggeredEffectData effect = weaponData.awkening.effect;

        if (effect == null) return;
        if (effect.trigger != trigger) return;
        if (effect.effectPrefab == null) return;

        if (Random.value > effect.triggerChance) return;

        WeaponTriggeredEffectActor instance = Instantiate(
            effect.effectPrefab,
            context.Position,
            Quaternion.identity
        );

        instance.HitResolved += HandleHitResolved;

        instance.Initialize(
            context,
            effect
        );
    }
}
