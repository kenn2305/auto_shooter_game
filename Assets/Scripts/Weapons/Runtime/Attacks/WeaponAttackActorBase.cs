using System;
using UnityEngine;

public abstract class WeaponAttackActorBase : MonoBehaviour
{
    private Vector3 authoredLocalScale;
    private bool capturedAuthoredScale;

    public event Action<WeaponHitResult> HitResolved;
    public float AppliedAreaMultiplier { get; private set; } = 1f;

    public abstract void Initialize(
        GameObject attackOwner,
        WeaponData weapon,
        WeaponLevelData level,
        CharacterCombatSystem stats,
        float animationSpeed
    );

    protected void ReportHit(WeaponHitResult result)
    {
        HitResolved?.Invoke(result);
    }

    protected float ApplyAreaScale(WeaponLevelData level, CharacterCombatSystem combatStats)
    {
        if (!capturedAuthoredScale)
        {
            authoredLocalScale = transform.localScale;
            capturedAuthoredScale = true;
        }

        AppliedAreaMultiplier = WeaponAreaUtility.ResolveMultiplier(level, combatStats);
        transform.localScale = authoredLocalScale * AppliedAreaMultiplier;
        return AppliedAreaMultiplier;
    }

    public virtual void ExecuteAttack() { }

    public virtual void RefreshLevel(WeaponLevelData newLevel) { }
}
