using System;
using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour, IDamageReceiver
{
    [Header("Health")]
    [SerializeField, Min(1)]
    [Tooltip("Used only when the character has no HP in its Character Stats. Replaced at runtime by the Max Health stat.")]
    private int maxHealth = 100;
    [SerializeField, Min(0)] private int currentHealth;

    [SerializeField]
    [Tooltip("Provides base HP and Max Health bonuses. Found in children when empty.")]
    private CharacterCombatSystem combatStats;

    [Header("Debug")]
    [SerializeField] private bool logAcceptedHits;

    private int fallbackMaxHealth;

    public event Action<PlayerHealth> Died;

    /// <summary>Raised for every heal that restored HP: (player, HP actually restored).</summary>
    public static event Action<PlayerHealth, int> AnyHealed;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0;
    public bool IsInvulnerable { get; set; }

    private void Awake()
    {
        if (combatStats == null)
        {
            combatStats = GetComponentInChildren<CharacterCombatSystem>(true);
        }

        fallbackMaxHealth = maxHealth;
        maxHealth = ResolveMaxHealth();
        ResetHealth();
    }

    private void OnEnable()
    {
        if (combatStats != null)
        {
            combatStats.StatsChanged += HandleStatsChanged;
            HandleStatsChanged();
        }
    }

    private void OnDisable()
    {
        if (combatStats != null)
        {
            combatStats.StatsChanged -= HandleStatsChanged;
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    /// <summary>Sets health to a saved value (resumed run), clamped to 1..max.</summary>
    public void RestoreHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 1, maxHealth);
    }

    public void Heal(int amount)
    {
        if (IsAlive && amount > 0)
        {
            int previous = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            if (currentHealth > previous)
            {
                AnyHealed?.Invoke(this, currentHealth - previous);
            }
        }
    }

    public DamageReceipt ReceiveDamage(in DamagePacket damage)
    {
        if (!IsAlive || IsInvulnerable || damage.Amount <= 0)
        {
            return DamageReceipt.Rejected;
        }

        int previousHealth = currentHealth;

        currentHealth = Mathf.Max(0, currentHealth - damage.Amount);

        int appliedAmount = previousHealth - currentHealth;
        bool killedTarget = previousHealth > 0 && currentHealth == 0;

        if (logAcceptedHits)
        {
            Debug.Log(
                $"[DAMAGE] Target={name} | "
                    + $"Type={damage.Type} | "
                    + $"Damage={appliedAmount} | "
                    + $"HP={currentHealth}/{maxHealth}",
                this
            );
        }

        if (killedTarget)
        {
            Died?.Invoke(this);
        }

        return new DamageReceipt(accepted: true, appliedAmount: appliedAmount, killedTarget: killedTarget);
    }

    private void HandleStatsChanged()
    {
        int newMaximum = ResolveMaxHealth();
        int gained = newMaximum - maxHealth;
        if (gained == 0)
        {
            return;
        }

        maxHealth = newMaximum;

        // Gaining Max Health also grants that much current health; losing it only caps the current value.
        if (IsAlive)
        {
            currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0, gained), 1, maxHealth);
        }
    }

    private int ResolveMaxHealth()
    {
        float value = combatStats != null ? combatStats.MaxHealth : 0f;
        return value > 0f ? Mathf.Max(1, Mathf.RoundToInt(value)) : fallbackMaxHealth;
    }
}
