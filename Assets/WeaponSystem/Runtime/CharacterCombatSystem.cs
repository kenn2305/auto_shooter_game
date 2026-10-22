using UnityEngine;

public sealed class CharacterCombatSystem : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField]
    private CharacterStats baseStats;

    [Header("Runtime Bonus")]
    [SerializeField]
    private float attackBonus;

    [SerializeField]
    private float criticalHitBonus;

    [SerializeField]
    private float hasteBonus;
    
    public float AttackPercent
    {
        get
        {
            float baseValue = baseStats != null ? baseStats.ATK : 100f;
            return Mathf.Max(0f, baseValue + attackBonus);
        }
    }

    public float CriticalHitPercent
    {
        get
        {
            float baseValue = baseStats != null ? baseStats.CRT : 0f;
            return Mathf.Max(0f, baseValue + criticalHitBonus);
        }
    }

    public float HastePercent
    {
        get
        {
            float baseValue = baseStats != null ? baseStats.Haste : 0f;
            return Mathf.Max(0f, baseValue + hasteBonus);
        }
    }

    public void AddAttackBonus(float bonus)
    {
        attackBonus += bonus;
    }

    public void AddCriticalHitBonus(float bonus)
    {
        criticalHitBonus += bonus;
    }

    public void AddHasteBonus(float bonus)
    {
        hasteBonus += bonus;
    }

    public void RemoveAttackBonus(float bonus)
    {
        attackBonus -= bonus;
    }

    public void RemoveCriticalHitBonus(float bonus)
    {
        criticalHitBonus -= bonus;
    }

    public void RemoveHasteBonus(float bonus)
    {
        hasteBonus -= bonus;
    }
}
