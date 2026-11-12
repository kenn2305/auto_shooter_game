public struct EnemySpawnRequest
{
    /// <summary>Prefab to spawn; null spawns the pool's default enemy.</summary>
    public EnemyPoolMember Prefab { get; }

    /// <summary>Forced requests (elite waves) ignore the alive cap and retry longer.</summary>
    public bool Forced { get; }

    public int RetryCount { get; private set; }

    public EnemySpawnRequest(int retryCount)
        : this(null, false, retryCount) { }

    public EnemySpawnRequest(EnemyPoolMember prefab, bool forced, int retryCount = 0)
    {
        Prefab = prefab;
        Forced = forced;
        RetryCount = retryCount;
    }

    public void IncreaseRetryCount()
    {
        RetryCount++;
    }
}
