using UnityEngine;

public readonly struct DamagePacket
{
    public int Amount { get; }
    public GameObject Source { get; }
    public DamageType Type { get; }
    public Vector2 HitPosition { get; }
    public bool IsCritical { get; }


    public DamagePacket(
        int amount,
        GameObject source,
        DamageType type,
        Vector2 hitPosition,
        bool isCritical)
    {
        Amount = Mathf.Max(0, amount);
        Source = source;
        Type = type;
        HitPosition = hitPosition;
        IsCritical = isCritical;
    }
    
}
