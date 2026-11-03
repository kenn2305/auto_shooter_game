using UnityEngine;

public readonly struct DamagePacket
{
    public int Amount { get; }
    public GameObject Source { get; }
    public DamageType Type { get; }
    public DamageElement Element { get; }
    public Vector2 HitPosition { get; }
    public bool IsCritical { get; }

    public DamagePacket(
        int amount,
        GameObject source,
        DamageType type,
        Vector2 hitPosition,
        bool isCritical,
        DamageElement element = DamageElement.Physical
    )
    {
        Amount = Mathf.Max(0, amount);
        Source = source;
        Type = type;
        Element = element;
        HitPosition = hitPosition;
        IsCritical = isCritical;
    }
}
