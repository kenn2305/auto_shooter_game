using System;
using UnityEngine;
using UnityEngine.Rendering;

public abstract class MeleeAttackActorBase : MonoBehaviour
{
    public event Action<MeleeHitResult> HitResolved;
    public abstract void Initialize(
        GameObject attackOwner,
        MeleeWeaponData weapon,
        MeleeWeaponLevelData level,
        CharacterCombatSystem stats,
        float animationSpeed
    );

    protected void ReportHit(MeleeHitResult result)
    {
        HitResolved?.Invoke(result);
    }

    public virtual void ExecuteAttack()
    {
        
    }

    public virtual void RefreshLevel(MeleeWeaponLevelData newLevel)
    {
        
    }
}
