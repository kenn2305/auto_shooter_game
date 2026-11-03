public enum WeaponHitRule
{
    // An enemy can be hit once during an attack animation.
    OnePerAttack,

    // An enemy can be hit once in each hitbox window.
    OnePerWindow,

    // An enemy can be hit again after the configured cooldown.
    RepeatWithCooldown,
}
