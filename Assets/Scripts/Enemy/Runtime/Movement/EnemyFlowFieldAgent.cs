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

    private Rigidbody2D body;
    private StatusEffectController statusEffects;
    private Vector2 navigationVelocity;
    private Vector2 externalVelocity;

    private Vector2 lastWalkablePosition;
    private bool hasLastWalkablePosition;

    private Vector2 recoveryTargetPosition;
    private bool hasRecoveryTargetPosition;

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
        statusEffects = GetComponent<StatusEffectController>();
    }

    private void OnEnable()
    {
        hasLastWalkablePosition = false;
        hasRecoveryTargetPosition = false;
        navigationVelocity = Vector2.zero;
        externalVelocity = Vector2.zero;
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

        EnemyCrowdManager.EnsureInstance()?.Register(this);
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

        Vector2 flowDirection = Vector2.zero;

        bool hasFlowDirection = flowField.TryGetDirection(body.position, out flowDirection);

        bool isRecovering = false;

        if (hasFlowDirection)
        {
            lastWalkablePosition = body.position;
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
        Vector2 targetVelocity = isStunned ? Vector2.zero : combinedDirection * moveSpeed * speedMultiplier;

        navigationVelocity = Vector2.MoveTowards(
            navigationVelocity,
            targetVelocity,
            acceleration * fixedDeltaTime
        );

        externalVelocity = Vector2.MoveTowards(externalVelocity, Vector2.zero, knockbackRecovery * fixedDeltaTime);

        body.linearVelocity = navigationVelocity + externalVelocity;
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
            if (hasLastWalkablePosition)
            {
                recoveryTargetPosition = lastWalkablePosition;
            }
            else if (!flowField.TryGetNearestWalkablePosition(body.position, out recoveryTargetPosition))
            {
                return false;
            }

            hasRecoveryTargetPosition = true;
        }

        Vector2 recoveryOffset = recoveryTargetPosition - body.position;

        if (recoveryOffset.sqrMagnitude <= 0.0001f)
        {
            hasRecoveryTargetPosition = false;
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
        }
    }

    private void OnDestroy()
    {
        EnemyCrowdManager.Instance?.Unregister(this);
    }
}
