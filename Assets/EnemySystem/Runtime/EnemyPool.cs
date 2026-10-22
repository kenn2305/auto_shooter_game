using UnityEngine;
using UnityEngine.Pool;

public sealed class EnemyPool : MonoBehaviour
{
    [Header("Setting")]
    [SerializeField]
    private EnemySpawnSetting setting;

    [Header("Hierarchy")]
    private Transform enemyContainer;

    private ObjectPool<EnemyPoolMember> pool;

    public int CountActive => pool != null ? pool.CountActive : 0;
    public int CountInActive => pool != null ? pool.CountInactive : 0;
    public int CountAll => pool != null ? pool.CountAll : 0;

    private void Awake()
    {
        if(setting == null)
        {
            enabled = false;
            return;
        }

        if (setting.enemyPrefabl == null)
        {
            enabled = false;
            return;
        }

        if (enemyContainer == null)
        {
            enemyContainer = transform;
        }

        pool = new ObjectPool<EnemyPoolMember>(
            createFunc: CreateEnemy,
            actionOnGet: null,
            actionOnRelease: OnReleaseEnemy,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: true,
            defaultCapacity: setting.initialPoolSize,
            maxSize: setting.maximumPoolSize
        );

        PreWarm();
    }

    public EnemyPoolMember Spawm(Vector2 position)
    {
        if (pool == null) return null;

        EnemyPoolMember enemy = pool.Get();
        enemy.SpawnAt(position);
        return enemy;
    }

    public void Release(EnemyPoolMember enemy)
    {
        if (pool == null || enemy == null || !enemy.IsSpawned) return;

        pool.Release(enemy);
    }
    
    private EnemyPoolMember CreateEnemy()
    {
        EnemyPoolMember enemy = Instantiate(setting.enemyPrefabl, enemyContainer);

        enemy.Blind(this);
        enemy.gameObject.SetActive(false);

        return enemy;
    }

    private void OnReleaseEnemy(EnemyPoolMember enemy)
    {
        enemy.DeSpawn();
    }

    private void OnDestroyEnemy(EnemyPoolMember enemy)
    {
        if (enemy != null)
        {
            Destroy(enemy.gameObject);
        }
    }

    private void PreWarm()
    {
        int count = setting.initialPoolSize;

        EnemyPoolMember[] buffer = new EnemyPoolMember[count];

        for ( int i = 0 ; i < count ; i++)
        {
            buffer[i] = pool.Get();
        }

        for( int i = count - 1 ; i >= 0 ; i--)
        {
            pool.Release(buffer[i]);
        }
    }
}
