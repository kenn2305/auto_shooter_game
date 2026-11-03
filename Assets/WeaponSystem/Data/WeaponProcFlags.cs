using System;

[Flags]
public enum WeaponProcFlags
{
    None = 0,
    CanTriggerOnHit = 1 << 0,
    CanTriggerOnKill = 1 << 1,
    Secondary = 1 << 2,

    PrimaryHit = CanTriggerOnHit | CanTriggerOnKill,
}
