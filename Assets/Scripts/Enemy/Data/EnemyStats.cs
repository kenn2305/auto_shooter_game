using UnityEngine;

/// <summary>
/// Per-enemy base stats, the enemy counterpart of <see cref="CharacterStats"/>.
/// One asset lives in each enemy folder (Enemies/&lt;Rank&gt;/&lt;Name&gt;/Data) and is read by
/// <see cref="EnemyStatsBinder"/> when the enemy wakes up.
/// Values are written by Tools > Enemies > Build From EnemyConfig.csv, but can be tuned here too.
/// </summary>
[CreateAssetMenu(fileName = "EnemyStats", menuName = "Scriptable Objects/EnemyStats")]
public class EnemyStats : ScriptableObject
{
    [Header("Rank")]
    public EnemyExperienceRank Rank = EnemyExperienceRank.Normal;

    [Header("HP")]
    [Tooltip("Maximum health.")]
    [Min(1)] public int HP = 100;

    [Header("ATK")]
    [Tooltip("Contact damage dealt to the player per hit.")]
    [Min(1)] public int ATK = 5;

    [Tooltip("Seconds between two contact hits while touching the player.")]
    [Min(0.05f)] public float AttackInterval = 1f;

    [Header("SPD")]
    [Tooltip("Movement speed in world units per second (player base is 5 x SPD%).")]
    [Min(0f)] public float SPD = 2.5f;

    [Header("EXP")]
    [Tooltip("Base EXP dropped on death. Use a value divisible by 4 for exact 25% increments.")]
    [Min(1)] public int EXP = 4;

    [Header("Behaviour")]
    [Tooltip("Never moves, ignores knockback and keeps its stats fixed (no phase scaling).")]
    public bool Stationary;

    [Header("Spawning")]
    [Tooltip("Relative chance to be picked when a random enemy of this rank spawns.")]
    [Min(0f)] public float SpawnWeight = 1f;

    [Tooltip("Maximum alive at the same time. 0 = no limit.")]
    [Min(0)] public int MaxAlive;
}
