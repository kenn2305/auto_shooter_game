using UnityEngine;

public static class DamageReceiverResolver
{
    public static bool TryResolve(Component component, out IDamageReceiver receiver)
    {
        receiver = null;

        if (component == null)
            return false;

        MonoBehaviour[] behaviours = component.GetComponentsInParent<MonoBehaviour>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDamageReceiver found)
            {
                receiver = found;
                return true;
            }
        }

        return false;
    }
}
