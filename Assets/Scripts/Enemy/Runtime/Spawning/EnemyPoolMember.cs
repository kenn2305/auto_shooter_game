using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyPoolMember : MonoBehaviour
{
    private EnemyPool owner;
    private Rigidbody2D body;
    private EnemyHealth health;
    private StatusEffectController statusEffects;

    public event Action<EnemyPoolMember> Dying;

    public bool IsSpawned { get; private set; }

    /// <summary>The prefab this instance was created from (its pool key).</summary>
    public EnemyPoolMember SourcePrefab { get; private set; }

    /// <summary>Base stats of this enemy, when it has an EnemyStatsBinder.</summary>
    public EnemyStats Stats => statsBinder != null ? statsBinder.Stats : null;

    private EnemyStatsBinder statsBinder;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        statusEffects = GetComponent<StatusEffectController>();
        statsBinder = GetComponent<EnemyStatsBinder>();

        if (health != null)
            health.Died += HandleDeath;
    }

    public void Bind(EnemyPool pool, EnemyPoolMember sourcePrefab = null)
    {
        owner = pool;
        SourcePrefab = sourcePrefab;
    }

    public void SpawnAt(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        body.position = position;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        statusEffects?.ClearAllStatuses(StatusRemovalReason.PoolReset);

        if (health != null)
            health.ResetHealth();

        IsSpawned = true;
        gameObject.SetActive(true);
    }

    public void Despawn()
    {
        if (!IsSpawned && !gameObject.activeSelf)
            return;

        IsSpawned = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        statusEffects?.ClearAllStatuses(StatusRemovalReason.PoolReset);

        gameObject.SetActive(false);
    }

    private void HandleDeath(EnemyHealth source)
    {
        Dying?.Invoke(this);

        if (owner != null)
            owner.Release(this);
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.Died -= HandleDeath;
    }
}
