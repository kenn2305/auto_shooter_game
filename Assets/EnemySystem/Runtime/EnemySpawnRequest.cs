public struct EnemySpawnRequest
{
    public int RetryCount { get; private set; }

    public EnemySpawnRequest(int retryCount)
    {
        RetryCount = retryCount;
    }

    public void IncreaseRetryCount()
    {
        RetryCount++;
    }
}