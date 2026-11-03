using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySpawnValidator
{
    private readonly Camera spawnCamera;
    private readonly FlowFieldService flowField;
    private readonly EnemySpawnSetting settings;

    private readonly ContactFilter2D occupiedFilter;

    private readonly List<Vector2> reservedPosition;

    public EnemySpawnValidator(
        Camera spawnCamera,
        FlowFieldService flowField,
        EnemySpawnSetting settings
    )
    {
        this.spawnCamera = spawnCamera;
        this.flowField = flowField;
        this.settings = settings;

        occupiedFilter = new ContactFilter2D();
        occupiedFilter.SetLayerMask(settings.occupiedMask);
        occupiedFilter.useTriggers = true;

        reservedPosition = new List<Vector2>(settings.maxSpawnPerFrame);
    }

    public void BeginSpawnBatch()
    {
        reservedPosition.Clear();
    }

    public bool TryFindSpawnPosition(out Vector2 spawnPosition)
    {
        spawnPosition = default;

        FlowFieldGrid grid = flowField.Grid;

        if (grid == null)
            return false;

        for (int attempt = 0; attempt < settings.maxAttemptsPerEnemy; ++attempt)
        {
            Vector2 rawPosition = CreateOutsideCameraCandidate();

            if (!TrySnapToFlowField(rawPosition, out Vector2 candidate))
                continue;

            if (IsInsideExpandedCamera(candidate))
                continue;
            if (OverlapReservedPosition(candidate))
                continue;
            if (IsOccupied(candidate))
                continue;

            reservedPosition.Add(candidate);
            spawnPosition = candidate;

            return true;
        }

        return false;
    }

    private Vector2 CreateOutsideCameraCandidate()
    {
        float cameraWorldWidth = spawnCamera.orthographicSize * 2f * spawnCamera.aspect;
        float cameraWorldHeight = spawnCamera.orthographicSize * 2f;

        float outsideDistance = Random.Range(
            settings.minOutsideDistance,
            settings.maxOutsideDistance
        );
        float horizontalViewportMargin = outsideDistance / cameraWorldWidth;
        float verticalViewportMargin = outsideDistance / cameraWorldHeight;

        float viewportX;
        float viewportY;

        int side = Random.Range(0, 4);

        switch (side)
        {
            case 0:
                viewportX = Random.value;
                viewportY = -verticalViewportMargin;
                break;
            case 1:
                viewportX = Random.value;
                viewportY = 1f + verticalViewportMargin;
                break;
            case 2:
                viewportX = -horizontalViewportMargin;
                viewportY = Random.value;
                break;
            default:
                viewportX = 1f + horizontalViewportMargin;
                viewportY = Random.value;
                break;
        }

        float cameraDepth = Mathf.Abs(spawnCamera.transform.position.z - settings.navigationPlaneZ);

        Vector3 worldPosition = spawnCamera.ViewportToWorldPoint(
            new Vector3(viewportX, viewportY, cameraDepth)
        );

        return worldPosition;
    }

    private bool TrySnapToFlowField(Vector2 rawPosition, out Vector2 spawnPosition)
    {
        spawnPosition = default;

        FlowFieldGrid grid = flowField.Grid;

        if (grid == null)
            return false;
        if (!grid.TryWorldToIndex(rawPosition, out int index))
            return false;
        if (!grid.IsWalkable(index))
            return false;

        if (grid.Integration[index] == FlowFieldGrid.UnreachableCost)
            return false;
        spawnPosition = grid.IndexToWorldCenter(index);

        return true;
    }

    private bool IsInsideExpandedCamera(Vector2 worldPosition)
    {
        Vector3 viewportPosition = spawnCamera.WorldToViewportPoint(
            new Vector3(worldPosition.x, worldPosition.y, settings.navigationPlaneZ)
        );

        if (viewportPosition.z <= 0f)
            return false;

        float cameraWorldWidth = spawnCamera.orthographicSize * 2f * spawnCamera.aspect;
        float cameraWorldHeight = spawnCamera.orthographicSize * 2f;
        float horizontalMargin = settings.visibleSafetyPadding / cameraWorldWidth;
        float verticalMargin = settings.visibleSafetyPadding / cameraWorldHeight;

        return viewportPosition.x > -horizontalMargin
            && viewportPosition.x <= 1f + horizontalMargin
            && viewportPosition.y >= -verticalMargin
            && viewportPosition.y <= 1f + verticalMargin;
    }

    private bool IsOccupied(Vector2 position)
    {
        Collider2D hit = Physics2D.defaultPhysicsScene.OverlapCapsule(
            position,
            settings.occupancySize,
            settings.capsuleDirection2,
            0f,
            occupiedFilter
        );

        return hit != null;
    }

    private bool OverlapReservedPosition(Vector2 candidate)
    {
        Vector2 requiredDistance =
            settings.occupancySize + Vector2.one * settings.spawnSeparationPadding;

        for (int i = 0; i < reservedPosition.Count; i++)
        {
            Vector2 delta = candidate - reservedPosition[i];

            bool overlapX = Mathf.Abs(delta.x) < requiredDistance.x;
            bool overlapY = Mathf.Abs(delta.y) < requiredDistance.y;

            if (overlapX && overlapY)
                return true;
        }

        return false;
    }
}
