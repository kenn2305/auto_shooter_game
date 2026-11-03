# Weapon On-Hit System

## Structure

- `Data/`: weapon and trigger configuration. These objects contain no per-target runtime state.
- `Effects/Data/`: reusable `WeaponEffectDefinition` and `StatusEffectDefinition` assets.
- `Effects/Runtime/`: status stacks, timers, target resolution, and receiver interfaces.
- `Runtime/WeaponController`: attack execution and trigger dispatch for melee, ranged, area, and future multishot actors.

## Configure an OnHit effect

1. Select a `WeaponData` asset.
2. Open the desired level and add an entry to `Triggered Effects`.
3. Set `Trigger` to `OnHit` and choose a `WeaponEffectDefinition` in `Effect`.
4. Use `Trigger Chance` and `Internal Cooldown` as required.
5. Leave `Allow Secondary Hits` disabled unless the effect is explicitly allowed to proc from DOT, mark reactions, explosions, or triggered projectiles.

Ready-to-use presets are in `Assets/GameContent/Weapons/Effects`:

- `ApplyBurnEffect`
- `ApplyPoisonEffect`
- `ApplyMarkEffect`
- `ApplySlowEffect`
- `ApplyStunEffect`
- `KnockbackEffect`

`MarkStatus` detonates at three stacks, consumes those stacks, and applies `MarkBurstDamage`.

## Runtime rules

- Primary weapon hits can trigger `OnHit` and `OnKill`.
- Secondary hits do not trigger `OnHit` by default.
- Effect chains are capped by `WeaponController`.
- DOT ticks can award `OnKill` but cannot proc `OnHit`.
- `StatusEffectController` clears all runtime state when a pooled target is disabled.
- `StatusEffectVisualController` creates and removes one particle visual per active status definition.
- `EnemyFlowFieldAgent` composes navigation and external knockback velocity instead of overwriting knockback on the next physics tick.

## Status visual feedback

Every `StatusEffectDefinition` exposes a `Visual Feedback` group in the Inspector:

- `Visual Style`: particle motion preset (`Mark`, `Slow`, `Stun`, `Burn`, or `Poison`). Use `None` to disable feedback.
- `Visual Color`: the particle tint.
- `Visual Offset`: local position relative to the affected enemy. Use a negative Y value for feet and a positive Y value for overhead feedback.
- `Visual Scale`: scales the whole particle effect without changing gameplay values.

The Enemy prefab owns the runtime `StatusEffectVisualController`. It listens to status apply/remove events, reuses the visual while stacks are refreshed, increases emission with stacks, and destroys the visual when the final matching status expires or is dispelled.

## Extending for ranged and multishot weapons

Create a new `WeaponAttackActorBase` implementation. It only needs to initialize from `WeaponData`/`WeaponLevelData` and report each accepted hit as a `WeaponHitResult`. `WeaponController` handles the shared cooldown, level, OnAttack, OnHit, OnKill, status, and proc-chain logic. Effects formerly treated as an awakening are configured directly in the target level's `Triggered Effects` list.
