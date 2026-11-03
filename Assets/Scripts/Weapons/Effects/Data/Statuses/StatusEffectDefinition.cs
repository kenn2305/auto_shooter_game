using UnityEngine;

public enum StatusStackPolicy
{
    RefreshDuration,
    AddStackAndRefresh,
    ExtendDuration,
    IndependentInstances,
}

public enum StatusStackScope
{
    SharedAcrossSources,
    SeparatePerSource,
}

public enum StatusRemovalReason
{
    Expired,
    Dispelled,
    PoolReset,
}

public enum StatusEffectVisualStyle
{
    None,
    Mark,
    Slow,
    Stun,
    Burn,
    Poison,
}

public abstract class StatusEffectDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string statusId;

    [Header("Lifetime")]
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField, Min(1)] private int maximumStacks = 1;
    [SerializeField] private StatusStackPolicy stackPolicy = StatusStackPolicy.RefreshDuration;
    [SerializeField] private StatusStackScope stackScope = StatusStackScope.SharedAcrossSources;

    [Header("Visual Feedback")]
    [SerializeField] private StatusEffectVisualStyle visualStyle;
    [SerializeField] private Color visualColor = Color.white;
    [SerializeField] private Vector3 visualOffset;
    [SerializeField, Min(0.1f)] private float visualScale = 1f;

    public string StatusId => string.IsNullOrWhiteSpace(statusId) ? name : statusId;
    public float Duration => Mathf.Max(0.01f, duration);
    public int MaximumStacks => Mathf.Max(1, maximumStacks);
    public StatusStackPolicy StackPolicy => stackPolicy;
    public StatusStackScope StackScope => stackScope;
    public StatusEffectVisualStyle VisualStyle => visualStyle;
    public Color VisualColor => visualColor;
    public Vector3 VisualOffset => visualOffset;
    public float VisualScale => Mathf.Max(0.1f, visualScale);

    public virtual float TickInterval => 0f;
    public virtual bool TickImmediately => false;
    public virtual float MovementSpeedMultiplier => 1f;
    public virtual bool StunsMovement => false;

    public virtual void OnApplied(
        StatusEffectController controller,
        ActiveStatusEffect runtime,
        in WeaponEffectContext context,
        int previousStacks
    ) { }

    public virtual void OnTick(StatusEffectController controller, ActiveStatusEffect runtime) { }

    public virtual void OnRemoved(
        StatusEffectController controller,
        ActiveStatusEffect runtime,
        StatusRemovalReason reason
    ) { }
}
