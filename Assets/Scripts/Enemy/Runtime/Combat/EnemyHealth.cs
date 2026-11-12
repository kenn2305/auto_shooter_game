using System;
using UnityEngine;

public sealed class EnemyHealth : MonoBehaviour, IDamageReceiver
{
    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0)] private int currentHealth;

    [Header("Debug")]
    [SerializeField] private bool logAcceptedHits = true;

    public event Action<EnemyHealth> Died;

    /// <summary>Raised for every enemy that dies, whoever owns it (used by the kill counter).</summary>
    public static event Action<EnemyHealth> AnyDied;

    /// <summary>Raised for every accepted hit on any enemy: (enemy, damage dealt, was it a critical hit).</summary>
    public static event Action<EnemyHealth, int, bool> AnyDamaged;

    private Collider2D bodyCollider;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0;

    /// <summary>World point just above the enemy's body, for damage numbers and similar markers.</summary>
    public Vector3 TopPosition =>
        bodyCollider != null
            ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.max.y, 0f)
            : transform.position + Vector3.up;

    private void Awake()
    {
        bodyCollider = GetComponent<Collider2D>();
        ResetHealth();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    /// <summary>Sets the maximum health (used by EnemyStatsBinder) and refills to it.</summary>
    public void SetMaxHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        ResetHealth();
    }

    public DamageReceipt ReceiveDamage(in DamagePacket damage)
    {
        if (!IsAlive || damage.Amount <= 0)
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
                $"[DAMAGE] "
                    + $"Target={name} | "
                    + $"Type={damage.Type} | "
                    + $"Damage={appliedAmount} | "
                    + $"Critical={damage.IsCritical} | "
                    + $"HP={currentHealth}/{maxHealth}",
                this
            );
        }

        // Shows the full hit (not just what was left of the HP bar), like most survivor games do.
        AnyDamaged?.Invoke(this, damage.Amount, damage.IsCritical);

        if (killedTarget)
        {
            Die();
        }

        return new DamageReceipt(accepted: true, appliedAmount: appliedAmount, killedTarget: killedTarget);
    }

    private void Die()
    {
        AnyDied?.Invoke(this);

        if (Died != null)
        {
            Died.Invoke(this);
        }
        else
        {
            // Fallback nếu Enemy chưa được quản lý bởi Pool.
            Destroy(gameObject);
        }
    }
}
