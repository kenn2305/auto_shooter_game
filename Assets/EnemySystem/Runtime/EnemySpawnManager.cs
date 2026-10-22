using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySpawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera spawnCamera;

    [SerializeField]
    private FlowFieldServiece flowField;

    [SerializeField]
    private EnemyPool enemyPool;

    [SerializeField]
    private EnemySpawnSetting settings;

    private Queue<EnemySpawnRequest> pedingRequests;
    private EnemySpawnValid valid;
    private float spawnTimer;

    public int PendingSpawnCount => pedingRequests != null ? pedingRequests.Count : 0;

    private void Awake()
    {
        if(spawnCamera == null) spawnCamera = Camera.main;
        if(flowField == null) flowField = FlowFieldServiece.Instance;
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        pedingRequests = new Queue<EnemySpawnRequest>(settings.maxPendingRequests);
        valid = new EnemySpawnValid(spawnCamera, flowField, settings);
    }

    private void Update()
    {
        if (flowField.Grid == null) return;

        CreateSpawnRequests();
        ProcessSpawnRequests();
    }

    private void CreateSpawnRequests()
    {
        int reservedEnemyCount = enemyPool.CountActive + pedingRequests.Count;

        if (reservedEnemyCount >= settings.maxAlive) return;

        spawnTimer += Time.deltaTime;

        while(spawnTimer >= settings.spawnInterval)
        {
            spawnTimer -= settings.spawnInterval;

            if (pedingRequests.Count >= settings.maxPendingRequests) break;

            reservedEnemyCount = enemyPool.CountActive + pedingRequests.Count;

            if(reservedEnemyCount >= settings.maxAlive) break;

            pedingRequests.Enqueue(new EnemySpawnRequest(0));
        }
    }

    private void ProcessSpawnRequests()
    {
        if (pedingRequests.Count == 0) return;

        valid.BeginSpawnBatch();

        int processCount = Math.Min(settings.maxSpawnPerFrame, pedingRequests.Count);

        for (int i = 0 ; i < processCount ; i++)
        {
            if (enemyPool.CountActive >= settings.maxAlive) break;

            EnemySpawnRequest request = pedingRequests.Dequeue();

            bool foundPosition = valid.TryFindSpawnPosition(out Vector2 position);

            if (foundPosition)
            {
                enemyPool.Spawm(position);
                continue;
            }

            request.IncreaseRetryCount();

            if (request.RetryCount < settings.maxRequestReties)
            {
                pedingRequests.Enqueue(request);
            }
        }
    }

    private bool ValidateReferences()
    {
        if (spawnCamera == null)
        {
            Debug.LogError(
                "EnemySpawnManager không tìm thấy Main Camera.",
                this
            );

            return false;
        }

        if (!spawnCamera.orthographic)
        {
            Debug.LogError(
                "Thiết kế spawn hiện tại yêu cầu " +
                "Orthographic Camera.",
                spawnCamera
            );

            return false;
        }

        if (flowField == null)
        {
            Debug.LogError(
                "EnemySpawnManager không tìm thấy " +
                "FlowFieldServiece.",
                this
            );

            return false;
        }

        if (enemyPool == null)
        {
            Debug.LogError(
                "EnemySpawnManager chưa được gán EnemyPool.",
                this
            );

            return false;
        }

        if (settings == null)
        {
            Debug.LogError(
                "EnemySpawnManager chưa được gán " +
                "EnemySpawnSettings.",
                this
            );

            return false;
        }

        return true;
    }
}
