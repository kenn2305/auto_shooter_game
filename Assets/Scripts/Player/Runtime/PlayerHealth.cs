using System;
using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour, IDamageReceiver
{
    [Header("Health")]
    [SerializeField, Min(1)]
    private int maxHealth = 100;

    [SerializeField, Min(0)]
    private int currentHealth;

    [Header("Debug")]
    [SerializeField]
    private bool logAcceptedHits;

    public event Action<PlayerHealth> Died;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0;

    private void Awake()
    {
        ResetHealth();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
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

        return new DamageReceipt(
            accepted: true,
            appliedAmount: appliedAmount,
            killedTarget: killedTarget
        );
    }
}
