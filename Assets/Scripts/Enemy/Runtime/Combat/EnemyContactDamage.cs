using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class EnemyContactDamage : MonoBehaviour
{
    [Header("Contact Damage")]
    [SerializeField, Min(1)] private int damagePerHit = 10;
    [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;
    [SerializeField] private LayerMask playerMask = 1 << 7;

    private float nextDamageTime;
    private GameObject damageSource;
    private Collider2D cachedPlayerCollider;
    private IDamageReceiver cachedReceiver;

    private void Awake()
    {
        EnemyPoolMember poolMember = GetComponentInParent<EnemyPoolMember>();

        damageSource =
            poolMember != null ? poolMember.gameObject
            : transform.parent != null ? transform.parent.gameObject : gameObject;

        Collider2D damageCollider = GetComponent<Collider2D>();

        if (!damageCollider.isTrigger)
        {
            Debug.LogError("EnemyContactDamage yêu cầu Collider2D bật Is Trigger.", this);
        }
    }

    /// <summary>Sets damage per hit and the hit interval (used by EnemyStatsBinder).</summary>
    public void Configure(int damage, float interval)
    {
        damagePerHit = Mathf.Max(1, damage);
        damageInterval = Mathf.Max(0.05f, interval);
    }

    private void OnEnable()
    {
        nextDamageTime = 0f;
        cachedPlayerCollider = null;
        cachedReceiver = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider2D other)
    {
        if (Time.time < nextDamageTime || (playerMask.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        if (other != cachedPlayerCollider)
        {
            if (!DamageReceiverResolver.TryResolve(other, out cachedReceiver))
            {
                return;
            }

            cachedPlayerCollider = other;
        }

        if (cachedReceiver == null)
        {
            return;
        }

        DamagePacket damage = new DamagePacket(
            damagePerHit,
            damageSource,
            DamageType.Contact,
            other.ClosestPoint(transform.position),
            isCritical: false
        );

        DamageReceipt receipt = cachedReceiver.ReceiveDamage(damage);

        if (receipt.Accepted)
        {
            nextDamageTime = Time.time + damageInterval;
        }
    }
}
