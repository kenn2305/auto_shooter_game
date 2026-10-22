using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class EnemyFlowFieldAgent : MonoBehaviour
{
    [Header("Flow Field")]
    [SerializeField]
    private FlowFieldServiece flowField;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 3f;

    [SerializeField, Min(0f)]
    private float acceleration = 15f;

    [Header("Crowd Separation")]
    [SerializeField, Min(0f)]
    private float separationRadius = 1.25f;

    [SerializeField, Min(0f)]
    private float separationWeight = 0.7f;

    private Rigidbody2D body;

    private Vector2 lastWalkablePosition;
    private bool hasLastWalkablePosition;

    private Vector2 recoveryTargetPosition;
    private bool hasRecoveryTargetPosition;

    public Vector2 Position =>
        body != null
            ? body.position
            : (Vector2)transform.position;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        hasLastWalkablePosition = false;
        hasRecoveryTargetPosition = false;
        EnemyCrowdManager.Instance?.Register(this);
    }

    private void Start()
    {
        if (flowField == null)
        {
            flowField = FlowFieldServiece.Instance;
        }

        if (flowField == null)
        {
            Debug.LogError(
                "Enemy không tìm thấy FlowFieldServiece.",
                this
            );

            enabled = false;
            return;
        }

        EnemyCrowdManager.Instance?.Register(this);
    }

    private void FixedUpdate()
    {
        if (flowField == null)
        {
            return;
        }

        Vector2 flowDirection = Vector2.zero;

        bool hasFlowDirection =
            flowField.TryGetDirection(
                body.position,
                out flowDirection
            );

        bool isRecovering = false;

        if (hasFlowDirection)
        {
            lastWalkablePosition = body.position;
            hasLastWalkablePosition = true;
            hasRecoveryTargetPosition = false;
        }
        else
        {
            isRecovering =
                TryGetRecoveryDirection(
                    out flowDirection
                );
        }

        Vector2 separationDirection = Vector2.zero;

        if (!isRecovering &&
            EnemyCrowdManager.Instance != null)
        {
            separationDirection =
                EnemyCrowdManager.Instance
                    .GetSeparation(
                        this,
                        separationRadius
                    );
        }

        Vector2 combinedDirection =
            flowDirection +
            separationDirection * separationWeight;

        combinedDirection =
            Vector2.ClampMagnitude(
                combinedDirection,
                1f
            );

        Vector2 targetVelocity =
            combinedDirection * moveSpeed;

        body.linearVelocity =
            Vector2.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime
            );
    }

    private bool TryGetRecoveryDirection(
        out Vector2 recoveryDirection)
    {
        recoveryDirection = Vector2.zero;

        if (!hasRecoveryTargetPosition)
        {
            if (hasLastWalkablePosition)
            {
                recoveryTargetPosition =
                    lastWalkablePosition;
            }
            else if (!flowField.TryGetNearestWalkablePosition(
                         body.position,
                         out recoveryTargetPosition))
            {
                return false;
            }

            hasRecoveryTargetPosition = true;
        }

        Vector2 recoveryOffset =
            recoveryTargetPosition - body.position;

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
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void OnDestroy()
    {
        EnemyCrowdManager.Instance?.Unregister(this);
    }
}
