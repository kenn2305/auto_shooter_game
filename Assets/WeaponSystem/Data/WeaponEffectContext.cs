using UnityEngine;

public readonly struct WeaponEffectContext
{
    public WeaponController Controller { get; }
    public GameObject Owner { get; }
    public WeaponData Weapon { get; }
    public WeaponLevelData Level { get; }
    public CharacterCombatSystem Stats { get; }

    public IDamageReceiver Target { get; }
    public GameObject TargetObject { get; }
    public Vector3 Position { get; }
    public Vector2 Direction { get; }

    public DamagePacket SourceDamage { get; }
    public DamageReceipt SourceReceipt { get; }
    public WeaponEffectOrigin Origin { get; }
    public WeaponProcFlags ProcFlags { get; }
    public int ChainDepth { get; }

    public bool IsSecondary => Origin != WeaponEffectOrigin.PrimaryAttack;

    public WeaponEffectContext(
        WeaponController controller,
        GameObject owner,
        WeaponData weapon,
        WeaponLevelData level,
        CharacterCombatSystem stats,
        IDamageReceiver target,
        GameObject targetObject,
        Vector3 position,
        Vector2 direction,
        DamagePacket sourceDamage,
        DamageReceipt sourceReceipt,
        WeaponEffectOrigin origin,
        WeaponProcFlags procFlags,
        int chainDepth
    )
    {
        Controller = controller;
        Owner = owner;
        Weapon = weapon;
        Level = level;
        Stats = stats;
        Target = target;
        TargetObject = targetObject;
        Position = position;
        Direction = direction;
        SourceDamage = sourceDamage;
        SourceReceipt = sourceReceipt;
        Origin = origin;
        ProcFlags = procFlags;
        ChainDepth = Mathf.Max(0, chainDepth);
    }

    public WeaponEffectContext CreateChild(
        WeaponEffectOrigin origin,
        bool canTriggerOnHit,
        bool canTriggerOnKill = true
    )
    {
        WeaponProcFlags flags = WeaponProcFlags.Secondary;
        if (canTriggerOnHit)
        {
            flags |= WeaponProcFlags.CanTriggerOnHit;
        }

        if (canTriggerOnKill)
        {
            flags |= WeaponProcFlags.CanTriggerOnKill;
        }

        return new WeaponEffectContext(
            Controller,
            Owner,
            Weapon,
            Level,
            Stats,
            Target,
            TargetObject,
            Position,
            Direction,
            SourceDamage,
            SourceReceipt,
            origin,
            flags,
            ChainDepth + 1
        );
    }
}
