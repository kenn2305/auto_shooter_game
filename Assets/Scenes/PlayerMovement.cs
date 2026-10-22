using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed = 5f;

    private Rigidbody2D _body;
    private Vector2 _movementInput;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        _body.linearVelocity = _movementInput * _speed;
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
