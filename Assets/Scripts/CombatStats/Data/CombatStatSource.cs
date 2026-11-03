using System;

[Flags]
public enum CombatStatSource
{
    None = 0,
    BaseStats = 1 << 0,
    InspectorBonus = 1 << 1,
    WeaponLevel = 1 << 2,
    Equipment = 1 << 3,
    Passive = 1 << 4,
    TemporaryBuff = 1 << 5,
    All = BaseStats | InspectorBonus | WeaponLevel | Equipment | Passive | TemporaryBuff,
}
