public sealed class ActiveStatusEffect
{
    public StatusEffectDefinition Definition { get; }
    public WeaponEffectContext SourceContext { get; private set; }
    public int SourceInstanceId { get; private set; }
    public int Stacks { get; set; }
    public float RemainingDuration { get; set; }
    public float TimeUntilTick { get; set; }

    public ActiveStatusEffect(
        StatusEffectDefinition definition,
        in WeaponEffectContext sourceContext
    )
    {
        Definition = definition;
        SourceContext = sourceContext;
        SourceInstanceId = ResolveSourceInstanceId(sourceContext);
        Stacks = 1;
        RemainingDuration = definition.Duration;
        TimeUntilTick = definition.TickImmediately ? 0f : definition.TickInterval;
    }

    public void RefreshSource(in WeaponEffectContext sourceContext)
    {
        SourceContext = sourceContext;
        SourceInstanceId = ResolveSourceInstanceId(sourceContext);
    }

    private static int ResolveSourceInstanceId(in WeaponEffectContext sourceContext)
    {
        return sourceContext.Owner != null ? sourceContext.Owner.GetInstanceID() : 0;
    }
}
