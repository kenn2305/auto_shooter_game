using System.Collections.Generic;

public sealed class ProjectileVolleyRuntimeState
{
    private readonly HashSet<IDamageReceiver> claimedTargets = new();

    public bool TryClaim(IDamageReceiver target)
    {
        return target != null && claimedTargets.Add(target);
    }
}
