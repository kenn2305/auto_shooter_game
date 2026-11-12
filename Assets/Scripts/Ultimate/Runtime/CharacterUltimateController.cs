using System;
using UnityEngine;

/// <summary>
/// Runs the character's configured skill when the player presses the ultimate key.
/// The attack goes through a WeaponController that this component owns, so a skill can use
/// every attack type, triggered effect and stat formula of the weapon system.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterUltimateController : MonoBehaviour
{
    [Header("Skill")]
    [SerializeField] private UltimateData ultimate;

    [Header("Owner References")]
    [SerializeField] private CharacterCombatSystem combatStats;
    [SerializeField] private PlayerHealth playerHealth;

    [SerializeField]
    [Tooltip("Attacks spawn under this transform. Empty uses the object holding CharacterCombatSystem.")]
    private Transform attackOrigin;

    [SerializeField] private MonoBehaviour aimProviderBehaviour;

    [Header("Runtime Debug")]
    [SerializeField, Min(0f)] private float remainingCooldown;
    [SerializeField, Min(0f)] private float remainingDuration;
    [SerializeField] private bool isActive;

    private WeaponController skillWeapon;

    public event Action Activated;
    public event Action Ended;

    public UltimateData Data => ultimate;
    public bool IsActive => isActive;
    public bool IsReady => ultimate != null && !isActive && remainingCooldown <= 0f;
    public float RemainingCooldown => remainingCooldown;
    public float RemainingDuration => remainingDuration;

    private void Awake()
    {
        ResolveReferences();
        if (ultimate == null)
        {
            return;
        }

        remainingCooldown = ultimate.initialCooldown;
        CreateSkillWeapon();
    }

    private void OnDisable()
    {
        if (isActive)
        {
            EndSkill();
        }
    }

    private void Update()
    {
        if (!isActive)
        {
            remainingCooldown = Mathf.Max(0f, remainingCooldown - Time.deltaTime);
            return;
        }

        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0f)
        {
            EndSkill();
        }
    }

    public bool TryActivate()
    {
        // The level-up draft pauses the game with timeScale 0; the skill must not fire behind it.
        if (!IsReady || !isActiveAndEnabled || Time.timeScale <= 0f)
        {
            return false;
        }

        isActive = true;
        remainingDuration = ultimate.duration;
        Activated?.Invoke();

        if (ultimate.duration <= 0f)
        {
            FireOnce();
            EndSkill();
            return true;
        }

        if (combatStats != null)
        {
            combatStats.SetStatBonuses(this, CombatStatSource.TemporaryBuff, ultimate.statBonuses);
        }

        if (playerHealth != null)
        {
            playerHealth.IsInvulnerable = ultimate.invulnerableWhileActive;
        }

        if (skillWeapon != null)
        {
            skillWeapon.ResetCooldown();
            skillWeapon.enabled = true;
        }

        return true;
    }

    private void FireOnce()
    {
        if (skillWeapon == null)
        {
            return;
        }

        skillWeapon.ResetCooldown();
        skillWeapon.enabled = true;
        skillWeapon.TryAttack();
        skillWeapon.enabled = false;
    }

    private void EndSkill()
    {
        isActive = false;
        remainingDuration = 0f;
        remainingCooldown = ultimate.cooldown;

        if (skillWeapon != null)
        {
            skillWeapon.enabled = false;
        }

        if (combatStats != null)
        {
            combatStats.RemoveStatBonuses(this);
        }

        if (playerHealth != null)
        {
            playerHealth.IsInvulnerable = false;
        }

        Ended?.Invoke();
    }

    private void CreateSkillWeapon()
    {
        WeaponData weapon = ultimate.attackWeapon;
        if (weapon == null)
        {
            return;
        }

        if (ultimate.duration <= 0f && weapon.GetLevel(0)?.attack?.lifetime == WeaponAttackActorLifetime.Persistent)
        {
            Debug.LogWarning(
                $"Ultimate '{ultimate.name}' uses a Persistent attack with Duration 0, so it will never attack.",
                this
            );
        }

        // Created inactive so that WeaponController.Awake runs after ConfigureRuntime has assigned its data.
        GameObject weaponObject = new($"{ultimate.name} Skill Weapon");
        weaponObject.SetActive(false);
        weaponObject.transform.SetParent(attackOrigin, false);

        WeaponController controller = weaponObject.AddComponent<WeaponController>();
        controller.IsSkillWeapon = true;
        if (!controller.ConfigureRuntime(weapon, combatStats, attackOrigin, aimProviderBehaviour))
        {
            Destroy(weaponObject);
            return;
        }

        // The object stays active so running attacks can keep reporting hits; only the component is toggled.
        controller.enabled = false;
        weaponObject.SetActive(true);
        skillWeapon = controller;
    }

    private void ResolveReferences()
    {
        if (combatStats == null)
        {
            combatStats = GetComponentInChildren<CharacterCombatSystem>(true);
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (attackOrigin == null)
        {
            attackOrigin = combatStats != null ? combatStats.transform : transform;
        }

        if (aimProviderBehaviour == null)
        {
            foreach (MonoBehaviour candidate in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (candidate is IWeaponAimProvider2D)
                {
                    aimProviderBehaviour = candidate;
                    break;
                }
            }
        }
    }
}
