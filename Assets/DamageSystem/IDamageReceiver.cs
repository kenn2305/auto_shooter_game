public interface IDamageReceiver
{
    bool IsAlive { get; }

    DamageReceipt ReceiveDamage(in DamagePacket damage);
}
