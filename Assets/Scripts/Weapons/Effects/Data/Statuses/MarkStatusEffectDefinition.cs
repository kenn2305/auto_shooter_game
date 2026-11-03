using System;
using System.Collections.Generic;
using UnityEngine;

public enum MarkReactionTrigger
{
    OnApplied,
    OnStackChanged,
    OnThresholdReached,
    OnExpired,
}

[Serializable]
public sealed class MarkReactionData
{
    public MarkReactionTrigger trigger = MarkReactionTrigger.OnThresholdReached;
    [Min(1)] public int requiredStacks = 1;
    [Min(0)] public int consumeStacks;

    public List<WeaponEffectDefinition> effects = new();
}

[CreateAssetMenu(fileName = "MarkStatus", menuName = "Weapon/Status/Mark")]
public sealed class MarkStatusEffectDefinition : StatusEffectDefinition
{
    [SerializeField] private List<MarkReactionData> reactions = new();

    public override void OnApplied(
        StatusEffectController controller,
        ActiveStatusEffect runtime,
        in WeaponEffectContext context,
        int previousStacks
    )
    {
        EvaluateReactions(controller, runtime, context, MarkReactionTrigger.OnApplied, previousStacks);

        if (runtime.Stacks != previousStacks)
        {
            EvaluateReactions(controller, runtime, context, MarkReactionTrigger.OnStackChanged, previousStacks);
        }

        EvaluateReactions(controller, runtime, context, MarkReactionTrigger.OnThresholdReached, previousStacks);
    }

    public override void OnRemoved(
        StatusEffectController controller,
        ActiveStatusEffect runtime,
        StatusRemovalReason reason
    )
    {
        if (reason != StatusRemovalReason.Expired)
        {
            return;
        }

        EvaluateReactions(controller, runtime, runtime.SourceContext, MarkReactionTrigger.OnExpired, runtime.Stacks);
    }

    private void EvaluateReactions(
        StatusEffectController controller,
        ActiveStatusEffect runtime,
        in WeaponEffectContext context,
        MarkReactionTrigger trigger,
        int previousStacks
    )
    {
        if (reactions == null)
        {
            return;
        }

        for (int i = 0; i < reactions.Count; i++)
        {
            MarkReactionData reaction = reactions[i];
            if (reaction == null || reaction.trigger != trigger)
            {
                continue;
            }

            int requiredStacks = Mathf.Max(1, reaction.requiredStacks);
            bool thresholdReached =
                trigger != MarkReactionTrigger.OnThresholdReached
                    ? runtime.Stacks >= requiredStacks
                    : previousStacks < requiredStacks && runtime.Stacks >= requiredStacks;

            if (!thresholdReached)
            {
                continue;
            }

            WeaponEffectContext reactionContext = context.CreateChild(
                WeaponEffectOrigin.MarkReaction,
                canTriggerOnHit: false
            );

            if (reaction.effects != null)
            {
                for (int effectIndex = 0; effectIndex < reaction.effects.Count; effectIndex++)
                {
                    reaction.effects[effectIndex]?.Apply(reactionContext);
                }
            }

            if (reaction.consumeStacks > 0)
            {
                bool remainsActive = controller.ConsumeStacks(runtime, reaction.consumeStacks);
                if (!remainsActive)
                {
                    return;
                }
            }
        }
    }
}
