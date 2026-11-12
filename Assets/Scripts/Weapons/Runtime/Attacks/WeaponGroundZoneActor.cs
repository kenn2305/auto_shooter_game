using UnityEngine;

/// <summary>
/// A damage zone left on the ground for a limited time, for example where a thrown projectile lands.
/// It reuses WeaponAuraActor for detection and damage, and reports hits straight to the owner's
/// WeaponController because it can outlive the attack that created it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WeaponAuraActor))]
public sealed class WeaponGroundZoneActor : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField, Min(0.01f)] private float tickInterval = 0.5f;

    private WeaponAuraActor aura;
    private float timeUntilTick;
    private bool initialized;

    private void Awake()
    {
        aura = GetComponent<WeaponAuraActor>();
    }

    public void Initialize(GameObject owner, WeaponData weapon, WeaponLevelData level, CharacterCombatSystem stats)
    {
        aura.Initialize(owner, weapon, level, stats, 1f);

        if (owner != null && owner.TryGetComponent(out WeaponController controller))
        {
            aura.HitResolved += controller.HandleHitResolved;
        }

        timeUntilTick = tickInterval;
        initialized = true;
        Destroy(gameObject, duration);
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        timeUntilTick -= Time.deltaTime;
        if (timeUntilTick <= 0f)
        {
            timeUntilTick += tickInterval;
            aura.ExecuteAttack();
        }
    }
}
