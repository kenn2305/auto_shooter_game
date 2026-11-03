using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyExperienceDropper : MonoBehaviour
{
    [SerializeField] private EnemyExperienceRank rank = EnemyExperienceRank.Normal;

    [SerializeField, Min(1)]
    [Tooltip(
        "EXP represented by a Normal drop before color multipliers. "
            + "Use a value divisible by 4 when exact 25% increments are required."
    )]
    private int baseExperience = 4;

    [SerializeField]
    [Tooltip(
        "Boss phase controllers should call DropPhaseExperience(). "
            + "Keep this enabled if the final death is also a rewarded phase."
    )]
    private bool dropRedOnBossDeath = true;

    private EnemyHealth health;
    private EnemyPoolMember poolMember;

    public EnemyExperienceRank Rank => rank;
    public int BaseExperience => baseExperience;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        poolMember = GetComponent<EnemyPoolMember>();
    }

    private void OnEnable()
    {
        if (poolMember != null)
        {
            poolMember.Dying += HandlePooledDeath;
        }
        else if (health != null)
        {
            health.Died += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (poolMember != null)
        {
            poolMember.Dying -= HandlePooledDeath;
        }
        else if (health != null)
        {
            health.Died -= HandleDeath;
        }
    }

    public void DropPhaseExperience()
    {
        if (rank == EnemyExperienceRank.Normal)
        {
            Debug.LogWarning("Normal enemies do not have red phase EXP.", this);
            return;
        }

        ExperiencePickupService.Instance.DropBossPhaseReward(transform.position, baseExperience);
    }

    private void HandleDeath(EnemyHealth source)
    {
        DropDeathReward();
    }

    private void HandlePooledDeath(EnemyPoolMember source)
    {
        DropDeathReward();
    }

    private void DropDeathReward()
    {
        ExperiencePickupService.Instance.DropEnemyReward(transform.position, baseExperience, rank, dropRedOnBossDeath);
    }
}
