using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Character select card. Text about the weapon is written here; the ultimate and the weapon evolutions
/// are read from the gameplay assets, so the menu always matches what the character really does.
/// </summary>
[CreateAssetMenu(fileName = "CharacterProfile", menuName = "Game/Character Profile")]
public sealed class CharacterProfile : ScriptableObject
{
    [Tooltip("Name of the character's root object in every stage scene (Lily, Zena, Iris).")]
    public string characterId;
    public string displayName;
    public string epithet;
    public Sprite portrait;
    [Tooltip("Idle animation frames for the big preview (taken from the character's idle clip).")]
    public Sprite[] idleFrames;
    [Min(1f)] public float idleFramesPerSecond = 8f;
    [Tooltip("Part of an idle frame the character actually covers (0-1, from the bottom-left), union of all " +
        "frames. The preview scales this to one common height so every character looks the same size, " +
        "whatever empty space its sprite sheet cells have. Filled by Tools > Menu > Setup Menus.")]
    public Rect previewContent = new Rect(0f, 0f, 1f, 1f);

    [Header("Stats shown on the card")]
    public int maxHealth = 100;
    [Range(1, 5)] public int attackRating = 3;
    [Range(1, 5)] public int speedRating = 3;

    [Header("Weapon")]
    public string weaponTitle;
    [TextArea(2, 4)] public string weaponDescription;
    [Tooltip("Main weapon(s). Zena has one per form; their evolutions are listed on the card.")]
    public List<WeaponData> weapons = new();

    [Header("Ultimate (one of the two)")]
    public UltimateData ultimate;
    public CharacterFormSwitchData formSwitch;

    public Sprite WeaponIcon => weapons.Count > 0 && weapons[0] != null ? weapons[0].icon : null;

    public string UltimateName =>
        formSwitch != null ? formSwitch.displayName : ultimate != null ? ultimate.displayName : "-";

    public string UltimateDescription =>
        formSwitch != null ? formSwitch.description : ultimate != null ? ultimate.description : string.Empty;

    public float UltimateCooldown =>
        formSwitch != null ? formSwitch.switchCooldown : ultimate != null ? ultimate.cooldown : 0f;

    /// <summary>(weapon name, evolution name, description) for every evolution of every main weapon.</summary>
    public IEnumerable<(WeaponData Weapon, WeaponEvolutionData Evolution)> Evolutions()
    {
        foreach (WeaponData weapon in weapons)
        {
            if (weapon == null || weapon.evolutions == null)
                continue;

            foreach (WeaponEvolutionData evolution in weapon.evolutions)
            {
                if (evolution != null && !string.IsNullOrWhiteSpace(evolution.evolutionID))
                    yield return (weapon, evolution);
            }
        }
    }

    public string FormsSummary()
    {
        if (formSwitch == null || formSwitch.forms.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (CharacterFormData form in formSwitch.forms)
        {
            if (sb.Length > 0)
                sb.Append(" / ");
            sb.Append(form.formName);
        }

        return sb.ToString();
    }
}
