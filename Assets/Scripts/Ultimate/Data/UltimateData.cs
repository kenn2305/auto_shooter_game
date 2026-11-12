using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UltimateData", menuName = "Character/Ultimate Data")]
public sealed class UltimateData : ScriptableObject
{
    [Header("Identity")]
    public string ultimateID;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Cooldown")]
    [Tooltip("Seconds before the skill can be used again, counted from the moment it ends.")]
    [Min(0f)] public float cooldown = 30f;

    [Tooltip("Seconds before the first use of a run. 0 means ready immediately.")]
    [Min(0f)] public float initialCooldown;

    [Header("Active Window")]
    [Tooltip(
        "Seconds the skill stays active. "
            + "0 means instant: Attack Weapon fires exactly once and Stat Bonuses have no effect."
    )]
    [Min(0f)] public float duration;

    [Header("Effects")]
    [Tooltip(
        "Attack used by the skill, configured like any weapon. Only its first level is used. "
            + "While active it attacks on its own Attack Interval, and a Persistent attack such as an Aura "
            + "exists only while the skill is active. A Persistent attack requires Duration above 0."
    )]
    public WeaponData attackWeapon;

    [Tooltip("Bonuses granted while the skill is active. Value 30 means +30% of the base stat. Max Health adds a flat number.")]
    public List<CombatStatBonus> statBonuses = new();

    [Tooltip("The character takes no damage while the skill is active. Has no effect when Duration is 0.")]
    public bool invulnerableWhileActive;
}
