using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every enemy prefab the spawn director may pick from, split by rank.
/// Rebuilt automatically by Tools > Enemies > Build From EnemyConfig.csv.
/// </summary>
[CreateAssetMenu(fileName = "EnemyRoster", menuName = "Scriptable Objects/EnemyRoster")]
public class EnemyRoster : ScriptableObject
{
    public List<EnemyPoolMember> normalEnemies = new List<EnemyPoolMember>();
    public List<EnemyPoolMember> eliteEnemies = new List<EnemyPoolMember>();
}
