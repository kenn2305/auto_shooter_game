using UnityEngine;

public readonly struct WeaponHitResult
{
    public IDamageReceiver Target { get; }
    public GameObject TargetObject { get; }
    public WeaponData Weapon { get; }
    public WeaponLevelData Level { get; }
    public DamagePacket Damage { get; }
    public Vector3 HitPosition { get; }
    public Vector2 Direction { get; }
    public DamageReceipt Receipt { get; }
    public WeaponEffectOrigin Origin { get; }
    public WeaponProcFlags ProcFlags { get; }
    public int ChainDepth { get; }

    public bool KilledTarget => Receipt.KilledTarget;

    public WeaponHitResult(
        IDamageReceiver target,
        GameObject targetObject,
        WeaponData weapon,
        WeaponLevelData level,
        DamagePacket damage,
        Vector3 hitPosition,
        Vector2 direction,
        DamageReceipt receipt,
        WeaponEffectOrigin origin,
        WeaponProcFlags procFlags,
        int chainDepth
    )
    {
        Target = target;
        TargetObject = targetObject;
        Weapon = weapon;
        Level = level;
        Damage = damage;
        HitPosition = hitPosition;
        Direction = direction;
        Receipt = receipt;
        Origin = origin;
        ProcFlags = procFlags;
        ChainDepth = Mathf.Max(0, chainDepth);
    }
}
