using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the run timeline from EnemyPhaseConfig.csv: tracks the current phase, exposes the stat
/// multipliers every enemy reads when it spawns, picks which normal enemy the spawner creates
/// and launches the elite waves.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class EnemyPhaseDirector : MonoBehaviour
{
    // Elite waves of an endless last phase are scheduled up to this run minute.
    private const float EndlessScheduleMinutes = 180f;

    public static EnemyPhaseDirector Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private TextAsset phaseConfig;
    [SerializeField] private EnemyRoster roster;

    [Header("References")]
    [SerializeField] private EnemySpawnManager spawnManager;

    [Header("Pool")]
    [SerializeField, Min(0)] private int prewarmPerNormalEnemy = 6;
    [SerializeField, Min(0)] private int prewarmPerEliteEnemy = 1;

    [Header("Stationary Enemies")]
    [SerializeField, Min(5f)]
    [Tooltip("Stationary enemies farther than this from the camera are recycled so they do not pile up behind the player.")]
    private float stationaryDespawnDistance = 40f;

    [Header("Debug")]
    [SerializeField, Min(0f)]
    [Tooltip("Start the run at this minute (skips earlier elite waves). Use it to test later phases.")]
    private float startAtMinute;

    [SerializeField] private bool logEvents = true;

    private struct EliteWave
    {
        public float Time;
        public int Count;
    }

    private sealed class RosterEntry
    {
        public EnemyPoolMember Prefab;
        public EnemyStats Stats;
    }

    private List<EnemyPhase> phases;
    private readonly List<EliteWave> eliteWaves = new List<EliteWave>();
    private readonly List<RosterEntry> normals = new List<RosterEntry>();
    private readonly List<RosterEntry> elites = new List<RosterEntry>();
    private readonly HashSet<EnemyPoolMember> stationaryPrefabs = new HashSet<EnemyPoolMember>();
    private readonly List<EnemyPoolMember> recycleBuffer = new List<EnemyPoolMember>();
    private readonly List<RosterEntry> pickBuffer = new List<RosterEntry>();

    private float elapsed;
    private int phaseIndex = -1;
    private int nextWaveIndex;
    private float nextRecycleCheck;

    public bool IsReady => phases != null && phaseIndex >= 0;
    public float ElapsedSeconds => elapsed;
    public int PhaseIndex => phaseIndex;
    public int PhaseCount => phases != null ? phases.Count : 0;
    public EnemyPhase CurrentPhase => IsReady ? phases[phaseIndex] : null;

    public float HealthMultiplier => IsReady ? CurrentPhase.HealthMultiplier * GrowthMultiplier : 1f;
    public float DamageMultiplier => IsReady ? CurrentPhase.DamageMultiplier * GrowthMultiplier : 1f;

    /// <summary>1 + Growth% x minutes spent in the current phase.</summary>
    public float GrowthMultiplier
    {
        get
        {
            if (!IsReady)
                return 1f;

            float minutesInPhase = Mathf.Max(0f, elapsed - CurrentPhase.StartSeconds) / 60f;
            return 1f + CurrentPhase.GrowthPerMinute * 0.01f * minutesInPhase;
        }
    }
    public float SpeedMultiplier => IsReady ? CurrentPhase.SpeedMultiplier : 1f;

    /// <summary>Raised when a new phase begins (also once for the first phase).</summary>
    public event Action<EnemyPhase> PhaseChanged;

    /// <summary>Raised with the number of elites when an elite wave is launched.</summary>
    public event Action<int> EliteWaveStarted;

    private EnemyPool Pool => spawnManager != null ? spawnManager.Pool : null;

    public int NormalAliveCount
    {
        get
        {
            EnemyPool pool = Pool;
            if (pool == null)
                return 0;

            int total = 0;
            for (int i = 0; i < normals.Count; i++)
                total += pool.CountActiveOf(normals[i].Prefab);
            return total;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("Có nhiều hơn một EnemyPhaseDirector trong scene.", this);

        Instance = this;

        if (spawnManager == null)
            spawnManager = GetComponent<EnemySpawnManager>();

        if (!LoadPhases() || !LoadRoster())
        {
            phases = null;
            enabled = false;
            return;
        }

        BuildEliteSchedule();
        SetElapsed(startAtMinute * 60f, skipEliteWaves: true);
    }

    private void Start()
    {
        EnemyPool pool = Pool;
        if (pool == null)
            return;

        foreach (RosterEntry entry in normals)
            pool.Prewarm(entry.Prefab, entry.Stats.MaxAlive > 0 ? Mathf.Min(entry.Stats.MaxAlive, prewarmPerNormalEnemy) : prewarmPerNormalEnemy);

        foreach (RosterEntry entry in elites)
            pool.Prewarm(entry.Prefab, prewarmPerEliteEnemy);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        UpdatePhase();
        LaunchDueEliteWaves();

        if (Time.time >= nextRecycleCheck)
        {
            nextRecycleCheck = Time.time + 1f;
            RecycleFarStationaryEnemies();
        }
    }

    // ---------------------------------------------------------------- spawning API

    /// <summary>Random normal enemy by SpawnWeight, skipping types at their MaxAlive cap.</summary>
    public EnemyPoolMember PickNormalPrefab() => PickWeighted(normals);

    /// <summary>Random elite by SpawnWeight, skipping types at their MaxAlive cap.</summary>
    public EnemyPoolMember PickElitePrefab() => PickWeighted(elites);

    public void SpawnEliteWave(int count)
    {
        if (spawnManager == null || count <= 0)
            return;

        int queued = 0;
        for (int i = 0; i < count; i++)
        {
            EnemyPoolMember prefab = PickElitePrefab();
            if (prefab == null)
                break;

            spawnManager.EnqueueForced(prefab);
            queued++;
        }

        if (logEvents)
            Debug.Log($"[EnemyPhase] {FormatTime(elapsed)} Elite wave: {queued} elite(s).", this);

        EliteWaveStarted?.Invoke(queued);
    }

    // ---------------------------------------------------------------- debug

    [ContextMenu("Skip To Next Phase")]
    private void SkipToNextPhase()
    {
        if (phases == null || phaseIndex + 1 >= phases.Count)
            return;

        SetElapsed(phases[phaseIndex + 1].StartSeconds, skipEliteWaves: true);
    }

    [ContextMenu("Spawn Elite Wave (current phase count)")]
    private void DebugSpawnEliteWave()
    {
        if (IsReady)
            SpawnEliteWave(Mathf.Max(1, CurrentPhase.EliteCount));
    }

    public void SetElapsed(float seconds, bool skipEliteWaves)
    {
        elapsed = Mathf.Max(0f, seconds);

        if (skipEliteWaves)
        {
            nextWaveIndex = 0;
            // A wave due exactly now still fires.
            while (nextWaveIndex < eliteWaves.Count && eliteWaves[nextWaveIndex].Time < elapsed)
                nextWaveIndex++;
        }

        UpdatePhase();
    }

    // ---------------------------------------------------------------- internals

    private void UpdatePhase()
    {
        if (phases == null)
            return;

        int index = 0;
        for (int i = 1; i < phases.Count; i++)
        {
            if (elapsed >= phases[i].StartSeconds)
                index = i;
        }

        if (index == phaseIndex)
            return;

        phaseIndex = index;
        EnemyPhase phase = phases[phaseIndex];

        if (logEvents)
        {
            Debug.Log(
                $"[EnemyPhase] {FormatTime(elapsed)} → {phase.Name}: HP x{phase.HealthMultiplier:0.##}, "
                    + $"ATK x{phase.DamageMultiplier:0.##}, SPD x{phase.SpeedMultiplier:0.##}, "
                    + $"+{phase.GrowthPerMinute:0.##}%/min, "
                    + $"{phase.SpawnsPerSecond:0.##}/s, max {phase.MaxAlive}",
                this
            );
        }

        PhaseChanged?.Invoke(phase);
    }

    private void LaunchDueEliteWaves()
    {
        while (nextWaveIndex < eliteWaves.Count && eliteWaves[nextWaveIndex].Time <= elapsed)
        {
            SpawnEliteWave(eliteWaves[nextWaveIndex].Count);
            nextWaveIndex++;
        }
    }

    private void BuildEliteSchedule()
    {
        eliteWaves.Clear();

        for (int i = 0; i < phases.Count; i++)
        {
            EnemyPhase phase = phases[i];
            if (phase.EliteCount <= 0)
                continue;

            float start = phase.StartSeconds;
            bool isLast = i + 1 >= phases.Count;
            float end = isLast ? EndlessScheduleMinutes * 60f : phases[i + 1].StartSeconds;

            if (phase.EliteEveryMinutes <= 0f)
            {
                // One wave when the phase ends; the endless last phase never ends.
                if (!isLast)
                    eliteWaves.Add(new EliteWave { Time = end, Count = phase.EliteCount });
                continue;
            }

            float step = phase.EliteEveryMinutes * 60f;
            for (float t = start + step; t < end - 0.01f; t += step)
                eliteWaves.Add(new EliteWave { Time = t, Count = phase.EliteCount });
        }

        eliteWaves.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    private bool LoadPhases()
    {
        if (phaseConfig == null)
        {
            Debug.LogError("EnemyPhaseDirector chưa được gán EnemyPhaseConfig.csv.", this);
            return false;
        }

        phases = EnemyPhase.ParseCsv(phaseConfig.text, out string error);
        if (phases == null)
        {
            Debug.LogError($"[EnemyPhase] {error}", this);
            return false;
        }

        return true;
    }

    private bool LoadRoster()
    {
        if (roster == null)
        {
            Debug.LogError("EnemyPhaseDirector chưa được gán EnemyRoster.", this);
            return false;
        }

        Collect(roster.normalEnemies, normals);
        Collect(roster.eliteEnemies, elites);

        if (normals.Count == 0)
        {
            Debug.LogError("EnemyRoster không có normal enemy nào có EnemyStats.", this);
            return false;
        }

        return true;
    }

    private void Collect(List<EnemyPoolMember> source, List<RosterEntry> target)
    {
        target.Clear();
        if (source == null)
            return;

        foreach (EnemyPoolMember prefab in source)
        {
            if (prefab == null || !prefab.TryGetComponent(out EnemyStatsBinder binder) || binder.Stats == null)
                continue;

            target.Add(new RosterEntry { Prefab = prefab, Stats = binder.Stats });
            if (binder.Stats.Stationary)
                stationaryPrefabs.Add(prefab);
        }
    }

    private EnemyPoolMember PickWeighted(List<RosterEntry> entries)
    {
        EnemyPool pool = Pool;
        pickBuffer.Clear();
        float totalWeight = 0f;

        foreach (RosterEntry entry in entries)
        {
            if (entry.Stats.SpawnWeight <= 0f)
                continue;
            if (entry.Stats.MaxAlive > 0 && pool != null && pool.CountActiveOf(entry.Prefab) >= entry.Stats.MaxAlive)
                continue;

            pickBuffer.Add(entry);
            totalWeight += entry.Stats.SpawnWeight;
        }

        if (pickBuffer.Count == 0)
            return null;

        float roll = UnityEngine.Random.value * totalWeight;
        foreach (RosterEntry entry in pickBuffer)
        {
            roll -= entry.Stats.SpawnWeight;
            if (roll <= 0f)
                return entry.Prefab;
        }

        return pickBuffer[pickBuffer.Count - 1].Prefab;
    }

    private void RecycleFarStationaryEnemies()
    {
        EnemyPool pool = Pool;
        Camera camera = Camera.main;
        if (pool == null || camera == null || stationaryPrefabs.Count == 0)
            return;

        Vector2 center = camera.transform.position;
        float maxSqr = stationaryDespawnDistance * stationaryDespawnDistance;

        recycleBuffer.Clear();
        foreach (EnemyPoolMember member in pool.ActiveMembers)
        {
            if (member.SourcePrefab != null
                && stationaryPrefabs.Contains(member.SourcePrefab)
                && ((Vector2)member.transform.position - center).sqrMagnitude > maxSqr)
            {
                recycleBuffer.Add(member);
            }
        }

        foreach (EnemyPoolMember member in recycleBuffer)
            pool.Release(member);
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}
