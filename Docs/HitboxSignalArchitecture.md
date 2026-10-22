# Animation hitbox signal architecture

`AnimationHitboxTrack` is a detector. It owns the authored frame/box data,
samples the configured animation, performs physics overlap queries, and emits
signals. It does not decide whether a contact is a valid hit and never deals
damage.

## Signal flow

```text
AnimationHitboxTrack
    -> ContactDetected(AnimationHitboxContact)
    -> combat receiver resolves IDamageable
    -> attack hit memory accepts/rejects the contact
    -> damage calculator calculates a result
    -> IDamageable.TakeDamage(result)
    -> weapon trigger processor reacts to a confirmed hit
```

The detector currently exposes:

```csharp
public event Action<AnimationHitboxContact> ContactDetected;
public event Action DetectionWindowOpened;
public event Action DetectionWindowClosed;
public HitboxTargetUnityEvent OnTargetDetected { get; }
```

`ContactDetected` is the main code API. It includes the source track, target,
collider, animation frame, box name, world center and world size. It may report
the same target again on a later physics scan. That is required for attacks
which repeat after a hit cooldown.

`OnTargetDetected` is a UnityEvent for simple Inspector wiring. It only passes
the target GameObject.

## Minimal code subscription

```csharp
using UnityEngine;

public sealed class HitboxSignalReceiver : MonoBehaviour
{
    [SerializeField] private AnimationHitboxTrack track;

    private void OnEnable()
    {
        track.ContactDetected += OnContactDetected;
    }

    private void OnDisable()
    {
        track.ContactDetected -= OnContactDetected;
    }

    private void OnContactDetected(AnimationHitboxContact contact)
    {
        Debug.Log(
            $"Target={contact.Target.name}, " +
            $"Frame={contact.FrameIndex}, " +
            $"Box={contact.BoxName}"
        );
    }
}
```

Always unsubscribe in `OnDisable`. Otherwise a reused object can receive the
same signal multiple times.

## IDamageable.cs

```csharp
public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(int amount);
}
```

## EnemyHealth.cs

```csharp
using UnityEngine;

public sealed class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    private int currentHealth;

    public bool IsAlive => currentHealth > 0;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (currentHealth == 0)
            Destroy(gameObject);
    }
}
```

## CharacterCombatStats.cs

The project assets currently use percentage points: ATK `120` means 120%, CRT
`5` means 5%, and Haste `7` means 7%.

```csharp
using UnityEngine;

public sealed class CharacterCombatStats : MonoBehaviour
{
    [SerializeField] private CharacterStats baseStats;

    public float AttackPercent { get; private set; }
    public float CriticalChancePercent { get; private set; }
    public float HastePercent { get; private set; }

    private void Awake()
    {
        AttackPercent = baseStats.ATK;
        CriticalChancePercent = baseStats.CRT;
        HastePercent = baseStats.Haste;
    }
}
```

Runtime buffs should modify runtime values. Do not modify the CharacterStats
asset during a run.

## DamageCalculator.cs

`AttackStepData.damageMultiplier` is the weapon damage coefficient. For
example, `1.8` means 180% weapon damage.

```csharp
using UnityEngine;

public readonly struct DamageResult
{
    public int Amount { get; }
    public bool IsCritical { get; }

    public DamageResult(int amount, bool isCritical)
    {
        Amount = amount;
        IsCritical = isCritical;
    }
}

public static class DamageCalculator
{
    private const float StandardDamage = 10f;
    private const float CriticalMultiplier = 1.5f;

    public static DamageResult Calculate(
        float weaponDamage,
        float attackPercent,
        float criticalChancePercent)
    {
        float amount = StandardDamage
            * weaponDamage
            * attackPercent / 100f;

        if (amount > 2f)
        {
            float variation = Mathf.Max(amount * 0.1f, 2f);
            amount = Random.Range(amount - variation, amount + variation);
        }

        bool critical = Random.value < criticalChancePercent / 100f;

        if (critical)
            amount *= CriticalMultiplier;

        return new DamageResult(
            Mathf.Max(1, Mathf.RoundToInt(amount)),
            critical
        );
    }
}
```

## AttackHitMemory.cs

This class owns gameplay hit policy. The detector must not contain this logic.

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class AttackHitMemory
{
    private readonly Dictionary<int, HashSet<IDamageable>> onceTargets = new();
    private readonly Dictionary<int, Dictionary<IDamageable, float>> nextTimes = new();
    private readonly Dictionary<AttackStepData, int> hitCounts = new();

    public void Reset()
    {
        onceTargets.Clear();
        nextTimes.Clear();
        hitCounts.Clear();
    }

    public bool TryAccept(AttackStepData step, IDamageable target)
    {
        if (step == null || step.attack == null || target == null || !target.IsAlive)
            return false;

        hitCounts.TryGetValue(step, out int count);

        if (step.attack.hitLimit >= 0 && count >= step.attack.hitLimit)
            return false;

        bool accepted = step.attack.hitRule switch
        {
            HitRule.OnePerHit => TryAcceptOnce(step.hitGroup, target),
            HitRule.RepeatWithCooldown => TryAcceptCooldown(
                step.hitGroup,
                target,
                step.attack.hitCooldown
            ),
            _ => false
        };

        if (accepted)
            hitCounts[step] = count + 1;

        return accepted;
    }

    private bool TryAcceptOnce(int hitGroup, IDamageable target)
    {
        if (!onceTargets.TryGetValue(hitGroup, out HashSet<IDamageable> targets))
        {
            targets = new HashSet<IDamageable>();
            onceTargets.Add(hitGroup, targets);
        }

        return targets.Add(target);
    }

    private bool TryAcceptCooldown(
        int hitGroup,
        IDamageable target,
        float cooldown)
    {
        if (!nextTimes.TryGetValue(
                hitGroup,
                out Dictionary<IDamageable, float> times))
        {
            times = new Dictionary<IDamageable, float>();
            nextTimes.Add(hitGroup, times);
        }

        if (times.TryGetValue(target, out float nextTime) &&
            Time.time < nextTime)
        {
            return false;
        }

        times[target] = Time.time + Mathf.Max(0f, cooldown);
        return true;
    }
}
```

## AnimationHitboxCombatReceiver.cs

This is the bridge between detection and gameplay. One receiver can be used for
the current `SlashAnim` track. A future WeaponController calls `BeginSequence`,
`UseStep` and `EndSequence`.

```csharp
using System;
using UnityEngine;

public readonly struct ConfirmedAttackHit
{
    public IDamageable Target { get; }
    public DamageResult Damage { get; }
    public AnimationHitboxContact Contact { get; }

    public ConfirmedAttackHit(
        IDamageable target,
        DamageResult damage,
        AnimationHitboxContact contact)
    {
        Target = target;
        Damage = damage;
        Contact = contact;
    }
}

public sealed class AnimationHitboxCombatReceiver : MonoBehaviour
{
    [SerializeField] private AnimationHitboxTrack track;
    [SerializeField] private CharacterCombatStats stats;

    private readonly AttackHitMemory hitMemory = new();
    private AttackStepData currentStep;
    private bool sequenceActive;

    public event Action<ConfirmedAttackHit> HitConfirmed;

    private void OnEnable()
    {
        track.ContactDetected += OnContactDetected;
    }

    private void OnDisable()
    {
        track.ContactDetected -= OnContactDetected;
    }

    public void BeginSequence()
    {
        hitMemory.Reset();
        currentStep = null;
        sequenceActive = true;
    }

    public void UseStep(AttackStepData step, LayerMask targetLayers)
    {
        currentStep = step;
        track.ConfigureDetection(targetLayers, step.areaMultiplier);
    }

    public void EndSequence()
    {
        sequenceActive = false;
        currentStep = null;
    }

    private void OnContactDetected(AnimationHitboxContact contact)
    {
        if (!sequenceActive || currentStep == null)
            return;

        IDamageable target = FindDamageable(contact.Collider);

        if (target == null || !hitMemory.TryAccept(currentStep, target))
            return;

        DamageResult damage = DamageCalculator.Calculate(
            currentStep.damageMultiplier,
            stats.AttackPercent,
            stats.CriticalChancePercent
        );

        target.TakeDamage(damage.Amount);
        HitConfirmed?.Invoke(new ConfirmedAttackHit(target, damage, contact));
    }

    private static IDamageable FindDamageable(Component collider)
    {
        if (collider == null)
            return null;

        MonoBehaviour[] behaviours =
            collider.GetComponentsInParent<MonoBehaviour>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDamageable damageable)
                return damageable;
        }

        return null;
    }
}
```

## Weapon controller responsibilities

A WeaponController should not subscribe directly to physics. It should:

1. Create one WeaponRuntime from WeaponData.
2. Count down the attack timer.
3. Calculate attack time as `baseTime / (1 + haste / 100)`.
4. Call `receiver.BeginSequence()` at the start of an attack.
5. For each AttackStepData, wait `delayBeforeAttack`, call
   `receiver.UseStep(step, weapon.targetLayerMask)`, then play the Animator
   state named by `step.attack.meleeAnimationName`.
6. Subscribe to `receiver.HitConfirmed` to run `ON_ATTACK_HIT` and `ON_KILL`
   triggered attacks.
7. Call `receiver.EndSequence()` when the sequence finishes.

Only confirmed hits may activate weapon triggers. Raw ContactDetected signals
must never directly activate damage, on-hit effects, lifesteal or projectiles.

