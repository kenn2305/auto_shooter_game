using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(StatusEffectController))]
public sealed class EnemyFlowFieldAgent : MonoBehaviour, IKnockbackReceiver
{
    [Header("Flow Field")]
    [SerializeField] private FlowFieldService flowField;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0f)] private float acceleration = 15f;

    [SerializeField, Min(0.02f)]
    [Tooltip("Seconds between steering updates (line of sight to the player / look-ahead on the field).")]
    private float steeringInterval = 0.12f;

    [Header("Knockback")]
    [SerializeField, Min(0f)]
    [Tooltip("How fast the launch stops. Higher means a shorter, harder hit.")]
    private float knockbackRecovery = 160f;

    [SerializeField, Min(0f)] private float maximumKnockbackSpeed = 60f;

    [SerializeField, Min(1f)]
    [Tooltip("Multiplies every knockback strength. Keep it at the square root of Recovery / 10 to keep the distance.")]
    private float knockbackBurstMultiplier = 4f;

    [Header("Crowd Separation")]
    [SerializeField, Min(0f)] private float separationRadius = 1.25f;
    [SerializeField, Min(0f)] private float separationWeight = 0.7f;

    [Header("Obstacles")]
    [SerializeField, Min(0f)]
    [Tooltip("Bodies wider than this (world units) never collide with map obstacles: the flow field is " +
        "baked for normal-sized enemies, so big ones (elites) still path around obstacles but cannot snag on them.")]
    private float passThroughObstaclesAboveWidth = 2.6f;

    [SerializeField, Min(0.05f)]
    [Tooltip("Seconds of walking at under a quarter of the wanted speed next to an obstacle before the enemy unsticks.")]
    private float stuckTime = 0.4f;

    [SerializeField, Min(0.05f)]
    [Tooltip("Seconds an unsticking enemy ignores map obstacles.")]
    private float unstickDuration = 0.8f;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private StatusEffectController statusEffects;
    private Vector2 navigationVelocity;
    private Vector2 externalVelocity;

    private Vector2 lastWalkablePosition;
    private bool hasLastWalkablePosition;

    private Vector2 recoveryTargetPosition;
    private bool hasRecoveryTargetPosition;

    private Vector2 steeringDirection;
    private bool hasSteeringDirection;
    private float nextSteeringTime;

    private Vector2 lastPosition;
    private float stuckTimer;
    private float unstickTimer;
    private bool alwaysPassThroughObstacles;
    private int obstacleMask;

    public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

    public float SeparationRadius => separationRadius;

    /// <summary>True once the agent found its flow field (EnemyCrowdManager only simulates ready agents).</summary>
    public bool IsReady => flowField != null;

    /// <summary>Sets the base movement speed in units per second (used by EnemyStatsBinder).</summary>
    public void SetMoveSpeed(float value)
    {
        moveSpeed = Mathf.Max(0f, value);
    }

    /// <summary>A stationary agent never moves and ignores knockback (used by EnemyStatsBinder).</summary>
    public void SetStationary(bool value)
    {
        isStationary = value;
    }

    public bool IsStationary => isStationary;

    private bool isStationary;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        statusEffects = GetComponent<StatusEffectController>();
    }

    private void OnEnable()
    {
        hasLastWalkablePosition = false;
        hasRecoveryTargetPosition = false;
        hasSteeringDirection = false;
        navigationVelocity = Vector2.zero;
        externalVelocity = Vector2.zero;
        stuckTimer = 0f;
        unstickTimer = 0f;
        lastPosition = Position;

        // Stagger steering updates so a wave spawned together does not recompute on the same step.
        nextSteeringTime = Time.time + Random.value * steeringInterval;

        if (flowField != null)
        {
            ConfigureObstacleCollision();
        }

        EnemyCrowdManager.EnsureInstance()?.Register(this);
    }

    private void Start()
    {
        if (flowField == null)
        {
            flowField = FlowFieldService.Instance;
        }

        if (flowField == null)
        {
            Debug.LogError("Enemy không tìm thấy FlowFieldService.", this);

            enabled = false;
            return;
        }

        ConfigureObstacleCollision();
        EnemyCrowdManager.EnsureInstance()?.Register(this);
    }

    private void ConfigureObstacleCollision()
    {
        obstacleMask = flowField.ObstacleMask.value;

        float width = bodyCollider != null ? bodyCollider.bounds.size.x : 0f;
        alwaysPassThroughObstacles = width > passThroughObstaclesAboveWidth;
        SetIgnoreObstacles(alwaysPassThroughObstacles);
    }

    /// <summary>
    /// One physics step of movement. Called by EnemyCrowdManager for every agent in a single batched
    /// FixedUpdate (no per-enemy Unity message), with the crowd separation already computed.
    /// </summary>
    public void Simulate(float fixedDeltaTime, Vector2 crowdSeparation)
    {
        if (flowField == null)
        {
            return;
        }

        if (isStationary)
        {
            navigationVelocity = Vector2.zero;
            externalVelocity = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 position = body.position;

        if (Time.time >= nextSteeringTime || !hasSteeringDirection)
        {
            nextSteeringTime = Time.time + steeringInterval;
            hasSteeringDirection = flowField.TryGetSteeringDirection(position, out steeringDirection);
        }

        Vector2 flowDirection = steeringDirection;
        bool isRecovering = false;

        if (hasSteeringDirection)
        {
            lastWalkablePosition = position;
            hasLastWalkablePosition = true;
            hasRecoveryTargetPosition = false;
        }
        else
        {
            isRecovering = TryGetRecoveryDirection(out flowDirection);
        }

        Vector2 separationDirection = isRecovering ? Vector2.zero : crowdSeparation;

        Vector2 combinedDirection = flowDirection + separationDirection * separationWeight;

        combinedDirection = Vector2.ClampMagnitude(combinedDirection, 1f);

        bool isStunned = statusEffects != null && statusEffects.IsMovementStunned;
        float speedMultiplier = statusEffects != null ? statusEffects.MovementSpeedMultiplier : 1f;
        float wantedSpeed = isStunned ? 0f : moveSpeed * speedMultiplier;
        Vector2 targetVelocity = combinedDirection * wantedSpeed;

        UpdateStuck(position, fixedDeltaTime, wantedSpeed);

        navigationVelocity = Vector2.MoveTowards(
            navigationVelocity,
            targetVelocity,
            acceleration * fixedDeltaTime
        );

        externalVelocity = Vector2.MoveTowards(externalVelocity, Vector2.zero, knockbackRecovery * fixedDeltaTime);

        body.linearVelocity = navigationVelocity + externalVelocity;
    }

    /// <summary>
    /// An enemy that keeps pushing but barely moves while an obstacle is next to it is caught on a
    /// corner the field could not see (its collider is wider than the baked agent). It then ignores map
    /// obstacles for a moment and slips past. Crowds jammed on the player are not affected: the check
    /// only runs next to blocked cells and away from the target.
    /// </summary>
    private void UpdateStuck(Vector2 position, float deltaTime, float wantedSpeed)
    {
        float moved = (position - lastPosition).magnitude;
        lastPosition = position;

        if (alwaysPassThroughObstacles)
        {
            return;
        }

        if (unstickTimer > 0f)
        {
            unstickTimer -= deltaTime;
            if (unstickTimer <= 0f)
            {
                SetIgnoreObstacles(false);
            }

            return;
        }

        bool pushing = wantedSpeed > 0.5f && externalVelocity.sqrMagnitude < 1f;
        bool barelyMoving = moved < wantedSpeed * deltaTime * 0.25f;

        if (pushing && barelyMoving && !IsNearTarget(position) && flowField.IsNearBlockedCell(position, 2))
        {
            stuckTimer += deltaTime;
        }
        else
        {
            stuckTimer = Mathf.Max(0f, stuckTimer - deltaTime);
        }

        if (stuckTimer >= stuckTime)
        {
            stuckTimer = 0f;
            unstickTimer = unstickDuration;
            SetIgnoreObstacles(true);
            hasSteeringDirection = false; // re-aim right away
        }
    }

    private bool IsNearTarget(Vector2 position)
    {
        Transform target = flowField.Target;
        return target != null && ((Vector2)target.position - position).sqrMagnitude < 9f;
    }

    private void SetIgnoreObstacles(bool ignore)
    {
        if (body == null)
        {
            return;
        }

        body.excludeLayers = ignore ? (body.excludeLayers | obstacleMask) : (body.excludeLayers & ~obstacleMask);
    }

    public void ApplyKnockback(Vector2 impulse)
    {
        if (!isActiveAndEnabled || isStationary || impulse.sqrMagnitude <= 0f)
        {
            return;
        }

        // The enemy loses its own momentum so the hit reads as being thrown backwards.
        navigationVelocity = Vector2.zero;
        externalVelocity = Vector2.ClampMagnitude(
            externalVelocity + impulse * knockbackBurstMultiplier,
            maximumKnockbackSpeed
        );
        body.WakeUp();
    }

    private bool TryGetRecoveryDirection(out Vector2 recoveryDirection)
    {
        recoveryDirection = Vector2.zero;

        if (!hasRecoveryTargetPosition)
        {
            // Nearest open cell first: walking back to the last open spot can mean turning away from the player.
            if (flowField.TryGetNearestWalkablePosition(body.position, out recoveryTargetPosition))
            {
                hasRecoveryTargetPosition = true;
            }
            else if (hasLastWalkablePosition)
            {
                recoveryTargetPosition = lastWalkablePosition;
                hasRecoveryTargetPosition = true;
            }
            else
            {
                return false;
            }
        }

        Vector2 recoveryOffset = recoveryTargetPosition - body.position;

        if (recoveryOffset.sqrMagnitude <= 0.0001f)
        {
            hasRecoveryTargetPosition = false;
            hasSteeringDirection = false;
            return false;
        }

        recoveryDirection = recoveryOffset.normalized;
        return true;
    }

    private void OnDisable()
    {
        EnemyCrowdManager.Instance?.Unregister(this);

        if (body != null)
        {
            navigationVelocity = Vector2.zero;
            externalVelocity = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            if (!alwaysPassThroughObstacles)
            {
                SetIgnoreObstacles(false);
            }
        }
    }

    private void OnDestroy()
    {
        EnemyCrowdManager.Instance?.Unregister(this);
    }
}
