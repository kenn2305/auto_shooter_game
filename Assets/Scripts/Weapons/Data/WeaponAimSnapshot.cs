using UnityEngine;

public interface IWeaponAimProvider2D
{
    bool TryGetAim(Vector2 origin, out Vector2 point, out Vector2 direction);
}

public readonly struct WeaponAimSnapshot
{
    public Vector2 Origin { get; }
    public Vector2 Point { get; }
    public Vector2 Direction { get; }

    public WeaponAimSnapshot(Vector2 origin, Vector2 point, Vector2 direction)
    {
        Origin = origin;
        Point = point;
        Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
    }
}
