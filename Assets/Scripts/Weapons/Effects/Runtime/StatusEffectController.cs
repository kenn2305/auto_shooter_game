using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StatusEffectController : MonoBehaviour, IStatusEffectReceiver
{
    private readonly List<ActiveStatusEffect> activeStatuses = new();

    private IDamageReceiver damageReceiver;

    public event Action<ActiveStatusEffect> StatusApplied;
    public event Action<StatusEffectDefinition> StatusRemoved;

    public IDamageReceiver DamageReceiver => damageReceiver;
    public int ActiveStatusCount => activeStatuses.Count;

    public float MovementSpeedMultiplier
    {
        get
        {
            float multiplier = 1f;
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                multiplier = Mathf.Min(
                    multiplier,
                    activeStatuses[i].Definition.MovementSpeedMultiplier
                );
            }

            return Mathf.Clamp01(multiplier);
        }
    }

    public bool IsMovementStunned
    {
        get
        {
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                if (activeStatuses[i].Definition.StunsMovement)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Awake()
    {
        ResolveDamageReceiver();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            if (i >= activeStatuses.Count)
            {
                continue;
            }

            ActiveStatusEffect status = activeStatuses[i];

            float tickInterval = status.Definition.TickInterval;
            if (tickInterval > 0f)
            {
                status.TimeUntilTick -= deltaTime;
                while (status.TimeUntilTick <= 0f && status.RemainingDuration > 0f)
                {
                    status.Definition.OnTick(this, status);
                    if (!isActiveAndEnabled || i >= activeStatuses.Count)
                    {
                        return;
                    }

                    status.TimeUntilTick += tickInterval;
                }
            }

            status.RemainingDuration -= deltaTime;

            if (status.RemainingDuration <= 0f && i < activeStatuses.Count)
            {
                RemoveAt(i, StatusRemovalReason.Expired, notifyDefinition: true);
            }
        }
    }

    private void OnDisable()
    {
        ClearAllStatuses(StatusRemovalReason.PoolReset);
    }

    public StatusApplyResult ApplyStatus(
        StatusEffectDefinition definition,
        in WeaponEffectContext context
    )
    {
        if (definition == null || damageReceiver == null || !damageReceiver.IsAlive)
        {
            return StatusApplyResult.Rejected;
        }

        ActiveStatusEffect runtime = FindMatchingStatus(definition, context);
        bool created = runtime == null;
        int previousStacks = runtime?.Stacks ?? 0;

        if (created || definition.StackPolicy == StatusStackPolicy.IndependentInstances)
        {
            runtime = new ActiveStatusEffect(definition, context);
            activeStatuses.Add(runtime);
            created = true;
            previousStacks = 0;
        }
        else
        {
            runtime.RefreshSource(context);
            ApplyStackPolicy(runtime);
        }

        definition.OnApplied(this, runtime, context, previousStacks);

        if (activeStatuses.Contains(runtime))
        {
            StatusApplied?.Invoke(runtime);
        }

        return new StatusApplyResult(
            accepted: true,
            created,
            previousStacks,
            runtime.Stacks
        );
    }

    public bool ConsumeStacks(ActiveStatusEffect runtime, int amount)
    {
        if (runtime == null || amount <= 0)
        {
            return runtime != null && activeStatuses.Contains(runtime);
        }

        int index = activeStatuses.IndexOf(runtime);
        if (index < 0)
        {
            return false;
        }

        runtime.Stacks = Mathf.Max(0, runtime.Stacks - amount);
        if (runtime.Stacks > 0)
        {
            return true;
        }

        RemoveAt(index, StatusRemovalReason.Dispelled, notifyDefinition: true);
        return false;
    }

    public bool HasStatus(StatusEffectDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        for (int i = 0; i < activeStatuses.Count; i++)
        {
            if (activeStatuses[i].Definition == definition)
            {
                return true;
            }
        }

        return false;
    }

    public void ClearAllStatuses(StatusRemovalReason reason = StatusRemovalReason.Dispelled)
    {
        bool notifyDefinitions = reason != StatusRemovalReason.PoolReset;
        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            RemoveAt(i, reason, notifyDefinitions);
        }
    }

    private ActiveStatusEffect FindMatchingStatus(
        StatusEffectDefinition definition,
        in WeaponEffectContext context
    )
    {
        if (definition.StackPolicy == StatusStackPolicy.IndependentInstances)
        {
            return null;
        }

        int sourceId = context.Owner != null ? context.Owner.GetInstanceID() : 0;
        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatusEffect candidate = activeStatuses[i];
            if (candidate.Definition != definition)
            {
                continue;
            }

            if (
                definition.StackScope == StatusStackScope.SharedAcrossSources
                || candidate.SourceInstanceId == sourceId
            )
            {
                return candidate;
            }
        }

        return null;
    }

    private static void ApplyStackPolicy(ActiveStatusEffect runtime)
    {
        StatusEffectDefinition definition = runtime.Definition;
        switch (definition.StackPolicy)
        {
            case StatusStackPolicy.AddStackAndRefresh:
                runtime.Stacks = Mathf.Min(definition.MaximumStacks, runtime.Stacks + 1);
                runtime.RemainingDuration = definition.Duration;
                break;

            case StatusStackPolicy.ExtendDuration:
                runtime.RemainingDuration += definition.Duration;
                break;

            case StatusStackPolicy.RefreshDuration:
            default:
                runtime.RemainingDuration = definition.Duration;
                break;
        }
    }

    private void RemoveAt(
        int index,
        StatusRemovalReason reason,
        bool notifyDefinition
    )
    {
        if (index < 0 || index >= activeStatuses.Count)
        {
            return;
        }

        ActiveStatusEffect status = activeStatuses[index];
        activeStatuses.RemoveAt(index);

        if (notifyDefinition)
        {
            status.Definition.OnRemoved(this, status, reason);
        }

        StatusRemoved?.Invoke(status.Definition);
    }

    private void ResolveDamageReceiver()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDamageReceiver found)
            {
                damageReceiver = found;
                return;
            }
        }

        Debug.LogError("StatusEffectController requires an IDamageReceiver on the same object.", this);
        enabled = false;
    }
}
