using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySpawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera spawnCamera;
    [SerializeField] private FlowFieldService flowField;
    [SerializeField] private EnemyPool enemyPool;
    [SerializeField] private EnemySpawnSetting settings;

    private Queue<EnemySpawnRequest> pendingRequests;
    private EnemySpawnValidator validator;
    private float spawnTimer;

    public int PendingSpawnCount => pendingRequests != null ? pendingRequests.Count : 0;

    private void Awake()
    {
        if (spawnCamera == null)
            spawnCamera = Camera.main;
        if (flowField == null)
            flowField = FlowFieldService.Instance;
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        pendingRequests = new Queue<EnemySpawnRequest>(settings.maxPendingRequests);
        validator = new EnemySpawnValidator(spawnCamera, flowField, settings);
    }

    private void Update()
    {
        if (flowField.Grid == null)
            return;

        CreateSpawnRequests();
        ProcessSpawnRequests();
    }

    private void CreateSpawnRequests()
    {
        int reservedEnemyCount = enemyPool.CountActive + pendingRequests.Count;

        if (reservedEnemyCount >= settings.maxAlive)
            return;

        spawnTimer += Time.deltaTime;

        while (spawnTimer >= settings.spawnInterval)
        {
            spawnTimer -= settings.spawnInterval;

            if (pendingRequests.Count >= settings.maxPendingRequests)
                break;

            reservedEnemyCount = enemyPool.CountActive + pendingRequests.Count;

            if (reservedEnemyCount >= settings.maxAlive)
                break;

            pendingRequests.Enqueue(new EnemySpawnRequest(0));
        }
    }

    private void ProcessSpawnRequests()
    {
        if (pendingRequests.Count == 0)
            return;

        validator.BeginSpawnBatch();

        int processCount = Math.Min(settings.maxSpawnPerFrame, pendingRequests.Count);

        for (int i = 0; i < processCount; i++)
        {
            if (enemyPool.CountActive >= settings.maxAlive)
                break;

            EnemySpawnRequest request = pendingRequests.Dequeue();

            bool foundPosition = validator.TryFindSpawnPosition(out Vector2 position);

            if (foundPosition)
            {
                enemyPool.Spawn(position);
                continue;
            }

            request.IncreaseRetryCount();

            if (request.RetryCount < settings.maxRequestRetries)
            {
                pendingRequests.Enqueue(request);
            }
        }
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
