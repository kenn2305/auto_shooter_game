using UnityEngine;

public readonly struct MeleeHitResult
{
    public IDamageReceiver Target { get; }
    public DamagePacket  Damage { get; }
    public Vector3 HitPosition { get; }
    public DamageReceipt Receipt { get; }

    public bool KilledTarget => Receipt.KilledTarget;


    public MeleeHitResult(
        IDamageReceiver  target, 
        DamagePacket damage, 
        Vector3 hitPosition, 
        DamageReceipt receipt)
    {
        Target = target;
        Damage = damage;
        HitPosition = hitPosition;
        Receipt = receipt;
    }
}
