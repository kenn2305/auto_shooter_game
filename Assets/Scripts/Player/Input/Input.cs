using UnityEngine;

public class Input : MonoBehaviour
{
    private PlayerInput _input;
    private PlayerInput.MotorActions _motor;

    [SerializeField]
    private LookAtMouse _lookAtMouse;

    [SerializeField]
    private PlayerMovement _playerMovement;

    private void Awake()
    {
        _input = new PlayerInput();
        _motor = _input.Motor;
    }

    private void Update()
    {
        Vector2 movementInput = _motor.Movement.ReadValue<Vector2>();
        _playerMovement.SetMovementInput(movementInput);
    }

    private void LateUpdate()
    {
        Vector2 mouseInput = _motor.Look.ReadValue<Vector2>();

        _lookAtMouse.LookAtMousePosition(mouseInput);
    }

    private void OnEnable()
    {
        _input.Enable();
    }

    private void OnDisable()
    {
        _input.Disable();
    }
}
