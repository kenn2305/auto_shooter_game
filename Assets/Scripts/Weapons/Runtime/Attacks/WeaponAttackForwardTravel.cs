using UnityEngine;

/// <summary>
/// Makes an attack actor start behind its owner and fly straight along the aim direction.
/// It leaves its parent, so the flight no longer follows the character. Forward is the local +X axis.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WeaponAttackActorBase))]
public sealed class WeaponAttackForwardTravel : MonoBehaviour
{
    [SerializeField, Min(0f)]
    [Tooltip("How far behind the character the attack appears, in world units.")]
    private float startBehindDistance = 8f;

    [SerializeField, Min(0f)] private float speed = 14f;

    private Vector3 direction;

    // Start runs after WeaponController has placed and initialized the actor.
    private void Start()
    {
        direction = transform.right;
        transform.SetParent(null, true);
        transform.position -= direction * startBehindDistance;
    }

    private void Update()
    {
        transform.position += direction * (speed * Time.deltaTime);
    }
}
