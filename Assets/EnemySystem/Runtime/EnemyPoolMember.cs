using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyPoolMember : MonoBehaviour
{
    private EnemyPool owner;
    private Rigidbody2D body;
    private EnemyHealth health;

    public bool IsSpawned {get; private set;}

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();

        if (health != null) health.Died += HandleDeath;
    }

    public void Blind(EnemyPool pool)
    {
        owner = pool;
    }

    public void SpawnAt(Vector2 position)
    {
        transform.position = new Vector3(position.x,position.y, transform.position.z);
        body.position = position;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        if (health != null) health.ResetHeath();

        IsSpawned = true;
        gameObject.SetActive(true);
    }

    public void DeSpawn()
    {
        if (!IsSpawned && !gameObject.activeSelf) return;

        IsSpawned = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        gameObject.SetActive(false);
    }

    private void HandleDeath(EnemyHealth source)
    {
        if (owner != null) owner.Release(this);
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= HandleDeath;
    }
}
