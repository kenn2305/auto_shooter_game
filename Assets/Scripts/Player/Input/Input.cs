using UnityEngine;

public class Input : MonoBehaviour
{
    private PlayerInput _input;
    private PlayerInput.MotorActions _motor;
    [SerializeField] private LookAtMouse _lookAtMouse;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private CharacterUltimateController _ultimate;
    [SerializeField] private CharacterFormSwitchController _formSwitch;

    private void Awake()
    {
        _input = PlayerInputLifetime.Create(); // with the player's rebinds from the Options screen
        _motor = _input.Motor;
    }

    private void OnDestroy()
    {
        PlayerInputLifetime.Release(ref _input);
    }

    private void Update()
    {
        if (GameplayPause.IsPaused)
        {
            // Menus (level-up draft) own the input: the character stands still and nothing fires.
            _playerMovement.SetMovementInput(Vector2.zero);
            return;
        }

        Vector2 movementInput = _motor.Movement.ReadValue<Vector2>();
        _playerMovement.SetMovementInput(movementInput);

        if (_motor.Ultimate.WasPressedThisFrame())
        {
            if (_formSwitch != null)
            {
                _formSwitch.TryActivate();
            }
            else if (_ultimate != null)
            {
                _ultimate.TryActivate();
            }
        }
    }

    private void LateUpdate()
    {
        if (GameplayPause.IsPaused)
        {
            return; // aim stays where it was when the menu opened
        }

        Vector2 mouseInput = _motor.Look.ReadValue<Vector2>();

        _lookAtMouse.LookAtMousePosition(mouseInput);
    }

    private void OnEnable()
    {
        _input?.Enable();
    }

    private void OnDisable()
    {
        _input?.Disable();
    }
}
