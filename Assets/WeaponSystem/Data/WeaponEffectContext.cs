using UnityEngine;

public readonly struct WeaponEffectContext
{
    public GameObject Owner { get; }
    public MeleeWeaponData Weapon { get; }
    public MeleeWeaponLevelData Level { get; }
    public CharacterCombatSystem Stats { get; }

    public IDamageReceiver Target { get; }
    public Vector3 Position { get; }
    public Vector2 Direction { get; }

    public DamagePacket SourceDamage { get; }

    public WeaponEffectContext(
        GameObject owner,
        MeleeWeaponData weapon,
        MeleeWeaponLevelData level,
        CharacterCombatSystem stats,
        IDamageReceiver target,
        Vector3 position,
        Vector2 direction,
        DamagePacket sourceDamage
    )
    {
        Owner = owner;
        Weapon = weapon;
        Level = level;
        Stats = stats;
        Target = target;
        Position = position;
        Direction = direction;
        SourceDamage = sourceDamage;
    }
}
