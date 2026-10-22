using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStats", menuName = "Scriptable Objects/CharacterStats")]
public class CharacterStats : ScriptableObject
{
    [Header("ATK")]
    [Tooltip("Multiplier for the character's attack power.")]
    [Min(0f)] public float ATK;

    [Header("HP")]
    [Tooltip("The character's maximum health.")]
    [Min(0f)] public float HP;

    [Header("SPD")]
    [Tooltip("Multiplier for the character's speed.")]
    [Min(0f)] public float SPD;

    [Header("PUR")]
    [Tooltip("Pickup range for the character.")]
    [Min(0f)] public float PUR;

    [Header("CRT")]
    [Tooltip("Critical hit chance for the character.")] 
    [Min(0f)] public float CRT;

    [Header("Haste")]
    [Tooltip("Haste value for the character, affecting action speed.")]
    [Min(0f)] public float Haste;
}
