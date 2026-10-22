using UnityEngine;

public enum MeleeAttackActorLifeTime
{
    Trasient,
    Persistent
}


[CreateAssetMenu(fileName = "MeleeAttackDefinition", menuName = "Scriptable Objects/MeleeAttackDefinition")]
public sealed class MeleeAttackDefinition : ScriptableObject
{
    [Header("Identity")]
    public string attackID;
    public string displayName;

    [Header("Execution")]
    public MeleeAttackActorLifeTime lifeTime = MeleeAttackActorLifeTime.Trasient;

    [Header("Attack Prefab")]
    public MeleeAttackActorBase attackPrefab;
    public string animatorStateName;
    // Prefab that contains the hitbox and animation for this attack

    public MeleeHitRule hitRule = MeleeHitRule.OnePerAttack;

    [Tooltip("Number enemy can be hit each attack, -1 = unlimited")]
    [Min(-1f)]
    public int hitLimit = -1;
    [Min(0.01f)]
    public float hitCooldown = 1f;
}
