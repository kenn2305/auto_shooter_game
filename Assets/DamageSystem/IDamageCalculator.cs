public interface IDamageCalculator
{
    DamagePacket Calculate(
        in DamageCalculationContext context
    );
}
