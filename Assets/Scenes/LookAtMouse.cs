using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LookAtMouse : MonoBehaviour
{
    [SerializeField] private Camera _mainCam;
    [SerializeField] private Transform _weaponController;

    public void LookAtMousePosition(Vector2 mousePosition)
    {
        Vector3 mousePos = (Vector2)_mainCam.ScreenToWorldPoint(mousePosition);
        float angleRad = Mathf.Atan2(mousePos.y - transform.position.y, mousePos.x - transform.position.x);
        float angleDeg = (180/ Mathf.PI) * angleRad - 90f;

        _weaponController.rotation = Quaternion.Euler(0, 0, angleDeg);
    }
}
