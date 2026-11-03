using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "EnemySpawnSetting", menuName = "Scriptable Objects/EnemySpawnSetting")]
public class EnemySpawnSetting : ScriptableObject
{
    [Header("Enemy")]
    [FormerlySerializedAs("enemyPrefabl")]
    public EnemyPoolMember enemyPrefab;

    [Header("Spawn Rate")]
    [Min(0.05f)] public float spawnInterval = 0.25f;
    [Min(1)] public int maxAlive = 200;
    [Min(1)] public int maxSpawnPerFrame = 4;
    [Min(1)] public int maxPendingRequests = 32;
    [Min(1)] public int maxAttemptsPerEnemy = 32;

    [Min(1)]
    [FormerlySerializedAs("maxRequestReties")]
    public int maxRequestRetries = 3;

    [Header("Camera")]
    [Min(0f)] public float minOutsideDistance = 2f;
    [Min(0f)] public float maxOutsideDistance = 5f;
    [Min(0f)] public float visibleSafetyPadding = 1.25f;
    public float navigationPlaneZ;

    [Header("Overlap")]
    public Vector2 occupancySize = new(1.1f, 2.1f);

    public CapsuleDirection2D capsuleDirection2 = CapsuleDirection2D.Vertical;
    [Min(0f)] public float spawnSeparationPadding = 0.1f;

    public LayerMask occupiedMask;

    [Header("Pool")]
    [Min(1)] public int initialPoolSize = 64;
    [Min(1)] public int maximumPoolSize = 300;
}
