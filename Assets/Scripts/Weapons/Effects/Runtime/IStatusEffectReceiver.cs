public interface IStatusEffectReceiver
{
    StatusApplyResult ApplyStatus(StatusEffectDefinition definition, in WeaponEffectContext context);

    void ClearAllStatuses(StatusRemovalReason reason = StatusRemovalReason.Dispelled);
}
