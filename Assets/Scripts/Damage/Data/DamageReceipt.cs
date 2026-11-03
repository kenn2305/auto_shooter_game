using UnityEngine;

public readonly struct DamageReceipt
{
    public bool Accepted { get; }
    public int AppliedAmount { get; }
    public bool KilledTarget { get; }

    public DamageReceipt(bool accepted, int appliedAmount, bool killedTarget)
    {
        Accepted = accepted;
        AppliedAmount = appliedAmount;
        KilledTarget = killedTarget;
    }

    public static DamageReceipt Rejected =>
        new DamageReceipt(accepted: false, appliedAmount: 0, killedTarget: false);
}
