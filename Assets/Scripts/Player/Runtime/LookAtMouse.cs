using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtMouse : MonoBehaviour, IWeaponAimProvider2D
{
    [SerializeField] private Camera _mainCam;
    [SerializeField] private Transform _weaponController;
    [SerializeField] private float gameplayPlaneZ;

    private Vector2 _worldAimPoint;
    private Vector2 _lastValidDirection = Vector2.up;
    private bool _hasWorldAimPoint;

    public Vector2 WorldAimPoint => _worldAimPoint;

    public void LookAtMousePosition(Vector2 mousePosition)
    {
        if (_mainCam == null)
        {
            _mainCam = Camera.main;
        }

        if (_mainCam == null)
        {
            return;
        }

        Vector3 screenPosition = new Vector3(
            mousePosition.x,
            mousePosition.y,
            Mathf.Abs(_mainCam.transform.position.z - gameplayPlaneZ)
        );
        Vector3 mouseWorldPosition = _mainCam.ScreenToWorldPoint(screenPosition);
        _worldAimPoint = new Vector2(mouseWorldPosition.x, mouseWorldPosition.y);
        _hasWorldAimPoint = true;

        Vector2 pivot = _weaponController != null ? _weaponController.position : transform.position;
        Vector2 direction = _worldAimPoint - pivot;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = _lastValidDirection;
        }
        else
        {
            _lastValidDirection = direction.normalized;
        }

        float angleRad = Mathf.Atan2(direction.y, direction.x);
        float angleDeg = (180 / Mathf.PI) * angleRad - 90f;

        if (_weaponController != null)
        {
            _weaponController.rotation = Quaternion.Euler(0, 0, angleDeg);
        }
    }

    public bool TryGetAim(Vector2 origin, out Vector2 point, out Vector2 direction)
    {
        if (_hasWorldAimPoint)
        {
            point = _worldAimPoint;
            direction = point - origin;
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                _lastValidDirection = direction;
                return true;
            }
        }

        direction = _lastValidDirection.sqrMagnitude > 0.0001f ? _lastValidDirection.normalized : Vector2.up;
        point = origin + direction;
        return false;
    }
}
