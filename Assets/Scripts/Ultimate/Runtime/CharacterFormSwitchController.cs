using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Switches the character between its forms when the player presses the ultimate key.
/// Only the weapon and the model change: the WeaponController keeps its level and chosen evolution.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterFormSwitchController : MonoBehaviour
{
    [Header("Skill")]
    [SerializeField] private CharacterFormSwitchData data;

    [Header("Owner References")]
    [SerializeField] private CharacterCombatSystem combatStats;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private WeaponController mainWeapon;
    [SerializeField] private PlayerAnimation playerAnimation;

    [Header("Models")]
    [Tooltip("One model per form, in the same order as Forms in the data.")]
    [SerializeField] private List<GameObject> formModels = new();

    [Header("Runtime Debug")]
    [SerializeField, Min(0)] private int currentFormIndex;
    [SerializeField, Min(0f)] private float remainingSwitchCooldown;
    [SerializeField, Min(0f)] private float remainingBuffDuration;
    [SerializeField, Min(0f)] private float remainingBuffCooldown;

    private float activeLifestealPercent;
    private float pendingHeal;

    public CharacterFormData CurrentForm =>
        data != null && currentFormIndex < data.forms.Count ? data.forms[currentFormIndex] : null;
    public CharacterFormSwitchData Data => data;
    public WeaponController MainWeapon => mainWeapon;
    public bool IsBuffActive => remainingBuffDuration > 0f;
    public float RemainingSwitchCooldown => remainingSwitchCooldown;
    public float RemainingBuffDuration => remainingBuffDuration;
    public float RemainingBuffCooldown => remainingBuffCooldown;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (combatStats != null)
        {
            combatStats.DamageDealt += HandleDamageDealt;
        }
    }

    private void OnDisable()
    {
        if (combatStats != null)
        {
            combatStats.DamageDealt -= HandleDamageDealt;
        }

        EndBuff();
    }

    private void Start()
    {
        // Waits for Start so that the WeaponController has finished its own Awake.
        if (data != null && data.forms.Count > 0)
        {
            ApplyForm(0);
        }
    }

    private void Update()
    {
        remainingSwitchCooldown = Mathf.Max(0f, remainingSwitchCooldown - Time.deltaTime);
        remainingBuffCooldown = Mathf.Max(0f, remainingBuffCooldown - Time.deltaTime);

        if (!IsBuffActive)
        {
            return;
        }

        remainingBuffDuration -= Time.deltaTime;
        if (remainingBuffDuration <= 0f)
        {
            EndBuff();
        }
    }

    public bool TryActivate()
    {
        // The level-up draft pauses the game with timeScale 0; the switch must not happen behind it.
        if (
            data == null
            || data.forms.Count < 2 || !isActiveAndEnabled || Time.timeScale <= 0f || remainingSwitchCooldown > 0f
        )
        {
            return false;
        }

        ApplyForm((currentFormIndex + 1) % data.forms.Count);
        remainingSwitchCooldown = data.switchCooldown;

        if (remainingBuffCooldown <= 0f)
        {
            GrantBuff(data.forms[currentFormIndex]);
        }

        return true;
    }

    private void ApplyForm(int formIndex)
    {
        currentFormIndex = formIndex;
        CharacterFormData form = data.forms[formIndex];

        if (mainWeapon != null)
        {
            mainWeapon.AttacksSuspended = form.weapon == null;
            if (form.weapon != null)
            {
                mainWeapon.SwapWeaponData(form.weapon);
            }
        }

        for (int i = 0; i < formModels.Count; i++)
        {
            if (formModels[i] != null)
            {
                formModels[i].SetActive(i == formIndex);
            }
        }

        if (playerAnimation != null && formIndex < formModels.Count && formModels[formIndex] != null)
        {
            playerAnimation.SetModel(
                formModels[formIndex].GetComponent<Animator>(),
                formModels[formIndex].GetComponent<SpriteRenderer>()
            );
        }
    }

    private void GrantBuff(CharacterFormData form)
    {
        remainingBuffDuration = data.buffDuration;
        remainingBuffCooldown = data.buffCooldown;
        activeLifestealPercent = form.lifestealPercent;
        pendingHeal = 0f;

        if (combatStats != null)
        {
            combatStats.SetStatBonuses(this, CombatStatSource.TemporaryBuff, form.statBonuses);
        }

        PushEnemiesAway(form);
    }

    private void EndBuff()
    {
        remainingBuffDuration = 0f;
        activeLifestealPercent = 0f;
        pendingHeal = 0f;

        if (combatStats != null)
        {
            combatStats.RemoveStatBonuses(this);
        }
    }

    private void PushEnemiesAway(CharacterFormData form)
    {
        if (form.knockbackRadius <= 0f || form.knockbackStrength <= 0f)
        {
            return;
        }

        Vector2 center = transform.position;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, form.knockbackRadius, data.knockbackLayerMask);
        HashSet<IKnockbackReceiver> pushed = new();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (
                !WeaponTargetResolver.TryResolveInterface(colliders[i].gameObject, out IKnockbackReceiver receiver)
                || !pushed.Add(receiver)
            )
            {
                continue;
            }

            Vector2 direction = (Vector2)colliders[i].bounds.center - center;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Random.insideUnitCircle.normalized;
            }

            receiver.ApplyKnockback(direction.normalized * form.knockbackStrength);
        }
    }

    private void HandleDamageDealt(int amount)
    {
        if (activeLifestealPercent <= 0f || playerHealth == null)
        {
            return;
        }

        // Health is a whole number, so small heals add up until they reach at least 1.
        pendingHeal += amount * activeLifestealPercent / 100f;
        int wholeHeal = Mathf.FloorToInt(pendingHeal);
        if (wholeHeal > 0)
        {
            pendingHeal -= wholeHeal;
            playerHealth.Heal(wholeHeal);
        }
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

        if (mainWeapon == null)
        {
            mainWeapon = GetComponentInChildren<WeaponController>(true);
        }

        if (playerAnimation == null)
        {
            playerAnimation = GetComponent<PlayerAnimation>();
        }
    }
}
