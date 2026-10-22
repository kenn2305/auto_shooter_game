using System;
using UnityEngine;

public abstract class WeaponTriggeredEffectActor : MonoBehaviour
{
    public event Action<MeleeHitResult> HitResolved;

    public abstract void Initialize(WeaponEffectContext context, WeaponTriggeredEffectData effectData);

    protected void ReportHit(MeleeHitResult result)
    {
        HitResolved?.Invoke(result);
    }
}
