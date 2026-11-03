using System;
using UnityEngine;

public abstract class WeaponTriggeredEffectActor : MonoBehaviour
{
    public event Action<WeaponHitResult> HitResolved;

    public abstract void Initialize(WeaponEffectContext context, WeaponTriggeredEffectData effectData);

    protected void ReportHit(WeaponHitResult result)
    {
        HitResolved?.Invoke(result);
    }
}
