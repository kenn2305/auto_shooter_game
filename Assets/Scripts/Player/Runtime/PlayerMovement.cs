using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed = 5f;

    [SerializeField]
    [Tooltip("Provides base SPD and movement-speed bonuses. Optional; 100% is used when absent.")]
    private CharacterCombatSystem _combatStats;

    private Rigidbody2D _body;
    private Vector2 _movementInput;

    public Vector2 MovementInput => _movementInput;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        if (_combatStats == null)
        {
            _combatStats = GetComponentInChildren<CharacterCombatSystem>(true);
        }
    }

    private void FixedUpdate()
    {
        float speedMultiplier = _combatStats != null
            ? _combatStats.GetMultiplier(CombatStatType.MovementSpeedPercent) : 1f;
        _body.linearVelocity = _movementInput * _speed * speedMultiplier;
    }

    public void SetMovementInput(Vector2 input)
    {
        _movementInput = Vector2.ClampMagnitude(input, 1f);
    }

    private void OnDisable()
    {
        if (_body != null)
        {
            _body.linearVelocity = Vector2.zero;
        }
    }
}
