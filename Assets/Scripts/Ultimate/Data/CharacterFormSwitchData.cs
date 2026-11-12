using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An Ultimate that cycles the character through its forms. Every form shares the same weapon level.
/// Entering a form grants that form's buff, unless the shared buff cooldown is still running.
/// </summary>
[CreateAssetMenu(fileName = "FormSwitchUltimate", menuName = "Character/Form Switch Ultimate")]
public sealed class CharacterFormSwitchData : ScriptableObject
{
    [Header("Identity")]
    public string ultimateID;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Switch")]
    [Tooltip("Seconds between two form switches.")]
    [Min(0f)] public float switchCooldown = 5f;

    [Header("Form Buff")]
    [Tooltip("Seconds the buff lasts. It keeps running if the character switches form again.")]
    [Min(0.01f)] public float buffDuration = 15f;

    [Tooltip("Seconds before another buff can be granted, counted from the moment the buff starts.")]
    [Min(0f)] public float buffCooldown = 30f;

    [Tooltip("Layers pushed away by a form's knockback.")]
    public LayerMask knockbackLayerMask;

    [Header("Forms")]
    [Tooltip("The first form is the starting form. The Ultimate key moves to the next one.")]
    public List<CharacterFormData> forms = new();
}

[Serializable]
public sealed class CharacterFormData
{
    public string formName;

    [Tooltip("Weapon of this form. Empty means the form cannot attack yet.")]
    public WeaponData weapon;

    [Header("Buff When Entering This Form")]
    [Tooltip("Value 50 means +50% of the base stat.")]
    public List<CombatStatBonus> statBonuses = new();

    [Tooltip("Percent of the damage dealt to each enemy that is healed while the buff lasts.")]
    [Range(0f, 100f)] public float lifestealPercent;

    [Tooltip("Enemies within this radius are pushed away once when entering the form. 0 means no knockback.")]
    [Min(0f)] public float knockbackRadius;

    [Min(0f)] public float knockbackStrength;
}
