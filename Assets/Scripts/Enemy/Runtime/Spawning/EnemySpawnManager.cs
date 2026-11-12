using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySpawnManager : MonoBehaviour
{
    // Forced requests (elite waves) keep retrying for about this many frames before giving up.
    private const int ForcedRequestRetries = 120;

    [Header("References")]
    [Tooltip("Optional: when empty or inactive, the active Main Camera is used (each character has its own).")]
    [SerializeField] private Camera spawnCamera;
    [SerializeField] private FlowFieldService flowField;
    [SerializeField] private EnemyPool enemyPool;
    [SerializeField] private EnemySpawnSetting settings;

    [SerializeField]
    [Tooltip("Drives spawn rate, alive cap and which enemy spawns. Without it the legacy EnemySpawnSetting values are used.")]
    private EnemyPhaseDirector phaseDirector;

    private Queue<EnemySpawnRequest> pendingRequests;
    private Queue<EnemySpawnRequest> forcedRequests;
    private EnemySpawnValidator validator;
    private float spawnTimer;

    public int PendingSpawnCount =>
        (pendingRequests != null ? pendingRequests.Count : 0) + (forcedRequests != null ? forcedRequests.Count : 0);

    public EnemyPool Pool => enemyPool;

    private void Awake()
    {
        ResolveSpawnCamera();
        if (flowField == null)
            flowField = FlowFieldService.Instance;
        if (phaseDirector == null)
            phaseDirector = GetComponent<EnemyPhaseDirector>();
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        pendingRequests = new Queue<EnemySpawnRequest>(settings.maxPendingRequests);
        forcedRequests = new Queue<EnemySpawnRequest>();
        validator = new EnemySpawnValidator(spawnCamera, flowField, settings);
    }

    private void Update()
    {
        if (flowField.Grid == null)
            return;

        if (!spawnCamera.isActiveAndEnabled && ResolveSpawnCamera())
            validator.SetCamera(spawnCamera);

        CreateSpawnRequests();
        ProcessSpawnRequests();
    }

    /// <summary>Queues one enemy that ignores the alive cap (used for elite waves).</summary>
    public void EnqueueForced(EnemyPoolMember prefab)
    {
        if (forcedRequests != null && prefab != null)
            forcedRequests.Enqueue(new EnemySpawnRequest(prefab, forced: true));
    }

    private bool UsesDirector => phaseDirector != null && phaseDirector.isActiveAndEnabled && phaseDirector.IsReady;

    private int MaxAlive => UsesDirector ? phaseDirector.CurrentPhase.MaxAlive : settings.maxAlive;

    private float SpawnInterval
    {
        get
        {
            if (!UsesDirector)
                return settings.spawnInterval;

            float rate = phaseDirector.CurrentPhase.SpawnsPerSecond;
            return rate > 0f ? 1f / rate : float.PositiveInfinity;
        }
    }

    // Normal enemies only: forced (elite) spawns do not count against the cap.
    private int NormalAliveCount => UsesDirector ? phaseDirector.NormalAliveCount : enemyPool.CountActive;

    private void CreateSpawnRequests()
    {
        float interval = SpawnInterval;
        if (float.IsInfinity(interval))
        {
            spawnTimer = 0f;
            return;
        }

        int maxAlive = MaxAlive;

        if (NormalAliveCount + pendingRequests.Count >= maxAlive)
            return;

        spawnTimer += Time.deltaTime;

        while (spawnTimer >= interval)
        {
            spawnTimer -= interval;

            if (pendingRequests.Count >= settings.maxPendingRequests)
                break;

            if (NormalAliveCount + pendingRequests.Count >= maxAlive)
                break;

            EnemyPoolMember prefab = null;
            if (UsesDirector)
            {
                prefab = phaseDirector.PickNormalPrefab();
                if (prefab == null)
                    break;
            }

            pendingRequests.Enqueue(new EnemySpawnRequest(prefab, forced: false));
        }
    }

    private void ProcessSpawnRequests()
    {
        if (pendingRequests.Count == 0 && forcedRequests.Count == 0)
            return;

        validator.BeginSpawnBatch();

        int budget = settings.maxSpawnPerFrame;

        // Elite waves first: they must not be starved by the normal stream.
        int forcedCount = Math.Min(budget, forcedRequests.Count);
        for (int i = 0; i < forcedCount; i++)
        {
            budget--;
            TrySpawn(forcedRequests.Dequeue(), forcedRequests, ForcedRequestRetries);
        }

        int normalCount = Math.Min(budget, pendingRequests.Count);
        for (int i = 0; i < normalCount; i++)
        {
            if (NormalAliveCount >= MaxAlive)
                break;

            TrySpawn(pendingRequests.Dequeue(), pendingRequests, settings.maxRequestRetries);
        }
    }

    private void TrySpawn(EnemySpawnRequest request, Queue<EnemySpawnRequest> retryQueue, int maxRetries)
    {
        if (validator.TryFindSpawnPosition(out Vector2 position))
        {
            if (request.Prefab != null)
                enemyPool.Spawn(request.Prefab, position);
            else
                enemyPool.Spawn(position);
            return;
        }

        request.IncreaseRetryCount();

        if (request.RetryCount < maxRetries)
            retryQueue.Enqueue(request);
    }

    /// <summary>
    /// Uses the camera that is actually rendering. Every character carries its own MainCam, so a camera
    /// assigned in the scene can belong to an inactive character; spawning around it would place enemies
    /// around a spot the player is not at. Returns true when the camera changed.
    /// </summary>
    private bool ResolveSpawnCamera()
    {
        if (spawnCamera != null && spawnCamera.isActiveAndEnabled)
            return false;

        Camera active = Camera.main; // enabled camera tagged MainCamera
        if (active == null || active == spawnCamera)
            return false;

        spawnCamera = active;
        return true;
    }

    private bool ValidateReferences()
    {
        if (spawnCamera == null)
        {
            Debug.LogError("EnemySpawnManager không tìm thấy Main Camera.", this);

            return false;
        }

        if (!spawnCamera.orthographic)
        {
            Debug.LogError(
                "Thiết kế spawn hiện tại yêu cầu " + "Orthographic Camera.",
                spawnCamera
            );

            return false;
        }

        if (flowField == null)
        {
            Debug.LogError("EnemySpawnManager không tìm thấy " + "FlowFieldService.", this);

            return false;
        }

        if (enemyPool == null)
        {
            Debug.LogError("EnemySpawnManager chưa được gán EnemyPool.", this);

            return false;
        }

        if (settings == null)
        {
            Debug.LogError("EnemySpawnManager chưa được gán " + "EnemySpawnSettings.", this);

            return false;
        }

        return true;
    }
}
