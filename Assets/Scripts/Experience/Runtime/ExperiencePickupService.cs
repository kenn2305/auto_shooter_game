using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public sealed class ExperiencePickupService : MonoBehaviour
{
    private static ExperiencePickupService instance;
    [SerializeField] private ExperienceSystemConfig config;
    [SerializeField] private PlayerExperience playerExperience;
    [SerializeField] private CharacterCombatSystem combatStats;

    private readonly List<ExperiencePickup> activePickups = new();
    private ObjectPool<ExperiencePickup> pool;
    private Transform playerTransform;
    private bool ownsRuntimeConfig;
    private float currentPickupRadius;
    private long normalEnemyRewardRolls;
    private readonly long[] spawnedPickupCounts = new long[5];

    public static ExperiencePickupService Instance => GetOrCreate();
    public int ActivePickupCount => activePickups.Count;
    public float CurrentPickupRadius => currentPickupRadius;
    public long NormalEnemyRewardRolls => normalEnemyRewardRolls;
    public ExperienceSystemConfig Config => config;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static ExperiencePickupService GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<ExperiencePickupService>();
        if (instance != null)
        {
            return instance;
        }

        GameObject serviceObject = new("Experience Pickup Service");
        instance = serviceObject.AddComponent<ExperiencePickupService>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ResolveConfig();
        ResolvePlayer();

        pool = new ObjectPool<ExperiencePickup>(
            CreatePickup,
            actionOnGet: null,
            actionOnRelease: pickup => pickup.Deactivate(),
            actionOnDestroy: pickup =>
            {
                if (pickup != null)
                {
                    Destroy(pickup.gameObject);
                }
            },
            // collectionCheck scans the whole pool on every Release (O(n) with up to 4096 entries);
            // pickups are only released from CollectAt, once each.
            collectionCheck: false,
            defaultCapacity: config.initialPoolSize,
            maxSize: config.maximumPoolSize
        );

        Prewarm();
    }

    private void Update()
    {
        if (playerExperience == null || playerTransform == null)
        {
            ResolvePlayer();
            if (playerExperience == null || playerTransform == null)
            {
                return;
            }
        }

        float pickupMultiplier = combatStats != null
            ? combatStats.GetMultiplier(CombatStatType.ExperiencePickupRangePercent) : 1f;
        currentPickupRadius = Mathf.Max(0f, config.basePickupRadius * pickupMultiplier);
        float pickupRadiusSquared = currentPickupRadius * currentPickupRadius;
        Vector2 playerPosition = playerTransform.position;
        float deltaTime = Time.deltaTime;
        long collectedExperience = 0;

        for (int i = activePickups.Count - 1; i >= 0; i--)
        {
            ExperiencePickup pickup = activePickups[i];
            if (pickup.Tick(playerPosition, pickupRadiusSquared, config, deltaTime))
            {
                collectedExperience += CollectAt(i);
            }
        }

        // One grant per frame: a magnet can collect hundreds of gems at once and every grant fires
        // the experience/level events. AddExperience handles several level-ups in one call.
        if (collectedExperience > 0)
        {
            playerExperience.AddExperience((int)System.Math.Min(collectedExperience, int.MaxValue));
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (ownsRuntimeConfig && config != null)
        {
            Destroy(config);
        }
    }

    public void DropEnemyReward(
        Vector3 position,
        int baseExperience,
        EnemyExperienceRank rank,
        bool dropRedForBossDeath = true
    )
    {
        switch (rank)
        {
            case EnemyExperienceRank.Normal:
                normalEnemyRewardRolls++;
                ExperiencePickupType regularType = config.RollNormalEnemyDrop(Random.value);
                Spawn(regularType, config.CalculateDropValue(baseExperience, regularType), position);
                break;
            case EnemyExperienceRank.Elite:
                SpawnRed(position, baseExperience);
                break;
            case EnemyExperienceRank.Boss when dropRedForBossDeath:
                SpawnRed(position, baseExperience);
                break;
        }

        TryDropRainbow(position);
    }

    public void DropBossPhaseReward(Vector3 position, int baseExperience)
    {
        SpawnRed(position, baseExperience);
        TryDropRainbow(position);
    }

    public void AttractAllExperience()
    {
        for (int i = 0; i < activePickups.Count; i++)
        {
            activePickups[i].BeginAttraction();
        }
    }

    private void Spawn(ExperiencePickupType type, int value, Vector3 position)
    {
        ExperiencePickup pickup = pool.Get();
        pickup.Activate(type, value, position, config);
        activePickups.Add(pickup);
        int typeIndex = (int)type;
        if (typeIndex >= 0 && typeIndex < spawnedPickupCounts.Length)
        {
            spawnedPickupCounts[typeIndex]++;
        }
    }

    private void TryDropRainbow(Vector3 position)
    {
        if (config.RollPercentChance(config.rainbowDropChancePercent, Random.value))
        {
            Spawn(ExperiencePickupType.RainbowMagnet, 0, position);
        }
    }

    private void SpawnRed(Vector3 position, int baseExperience)
    {
        Spawn(ExperiencePickupType.Red, config.CalculateDropValue(baseExperience, ExperiencePickupType.Red), position);
    }

    public long GetSpawnedPickupCount(ExperiencePickupType type)
    {
        int typeIndex = (int)type;
        return typeIndex >= 0 && typeIndex < spawnedPickupCounts.Length ? spawnedPickupCounts[typeIndex] : 0L;
    }

    /// <summary>Removes the pickup (swap-remove, O(1)) and returns the experience it gives.</summary>
    private int CollectAt(int index)
    {
        ExperiencePickup pickup = activePickups[index];
        int lastIndex = activePickups.Count - 1;
        activePickups[index] = activePickups[lastIndex];
        activePickups.RemoveAt(lastIndex);

        int value = 0;
        if (pickup.Type == ExperiencePickupType.RainbowMagnet)
        {
            AttractAllExperience();
        }
        else
        {
            value = pickup.Value;
        }

        pool.Release(pickup);
        return value;
    }

    private ExperiencePickup CreatePickup()
    {
        GameObject pickupObject = new("EXP Pickup");
        pickupObject.transform.SetParent(transform, false);
        pickupObject.AddComponent<SpriteRenderer>();
        pickupObject.AddComponent<CircleCollider2D>();
        ExperiencePickup pickup = pickupObject.AddComponent<ExperiencePickup>();
        pickupObject.SetActive(false);
        return pickup;
    }

    private void Prewarm()
    {
        int count = Mathf.Max(1, config.initialPoolSize);
        ExperiencePickup[] buffer = new ExperiencePickup[count];
        for (int i = 0; i < count; i++)
        {
            buffer[i] = pool.Get();
        }

        for (int i = count - 1; i >= 0; i--)
        {
            pool.Release(buffer[i]);
        }
    }

    private void ResolveConfig()
    {
        if (config == null)
        {
            config = Resources.Load<ExperienceSystemConfig>(ExperienceSystemConfig.ResourceName);
        }

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ExperienceSystemConfig>();
            ownsRuntimeConfig = true;
            Debug.LogWarning(
                "ExperienceSystemConfig was not found in Resources; using runtime defaults.",
                this
            );
        }
    }

    private void ResolvePlayer()
    {
        if (playerExperience == null)
        {
            playerExperience = FindFirstObjectByType<PlayerExperience>();
        }

        if (playerExperience == null)
        {
            playerTransform = null;
            combatStats = null;
            return;
        }

        playerTransform = playerExperience.transform;
        if (combatStats == null)
        {
            combatStats = playerExperience.GetComponentInChildren<CharacterCombatSystem>(true);
        }
    }
}
