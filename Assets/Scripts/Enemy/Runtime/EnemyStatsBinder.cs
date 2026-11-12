using UnityEngine;

/// <summary>
/// Pushes an <see cref="EnemyStats"/> asset into the enemy's components, the same way the player reads
/// its CharacterStats. Re-applied on every spawn so the current <see cref="EnemyPhaseDirector"/> phase
/// multipliers (HP / ATK / SPD) take effect; stationary enemies keep their base stats.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class EnemyStatsBinder : MonoBehaviour
{
    [SerializeField] private EnemyStats stats;

    public EnemyStats Stats => stats;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        if (stats == null)
        {
            Debug.LogWarning("EnemyStatsBinder has no EnemyStats assigned.", this);
            return;
        }

        EnemyPhaseDirector director = EnemyPhaseDirector.Instance;
        bool scaled = !stats.Stationary && director != null && director.IsReady;

        float healthMultiplier = scaled ? director.HealthMultiplier : 1f;
        float damageMultiplier = scaled ? director.DamageMultiplier : 1f;
        float speedMultiplier = scaled ? director.SpeedMultiplier : 1f;

        if (TryGetComponent(out EnemyHealth health))
        {
            health.SetMaxHealth(Mathf.RoundToInt(stats.HP * healthMultiplier));
        }

        if (TryGetComponent(out EnemyFlowFieldAgent agent))
        {
            agent.SetStationary(stats.Stationary);
            agent.SetMoveSpeed(stats.Stationary ? 0f : stats.SPD * speedMultiplier);
        }

        if (TryGetComponent(out EnemyExperienceDropper dropper))
        {
            dropper.Configure(stats.Rank, stats.EXP);
        }

        int damage = Mathf.Max(1, Mathf.RoundToInt(stats.ATK * damageMultiplier));
        foreach (EnemyContactDamage contact in GetComponentsInChildren<EnemyContactDamage>(true))
        {
            contact.Configure(damage, stats.AttackInterval);
        }
    }
}
