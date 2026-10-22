public enum MeleeHitRule
{
    //Enemy hit once per attack in single animation
    OnePerAttack,
    //Enemy hit once per attack in each hitbox window
    OnePerWindow,
    //Enemy hit after duration when in a dps hitbox
    RepeatWithCooldown
}