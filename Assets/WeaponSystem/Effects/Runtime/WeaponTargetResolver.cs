using UnityEngine;

public static class WeaponTargetResolver
{
    public static bool TryResolveInterface<T>(GameObject target, out T result)
        where T : class
    {
        result = null;
        if (target == null)
        {
            return false;
        }

        MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T found)
            {
                result = found;
                return true;
            }
        }

        return false;
    }
}
