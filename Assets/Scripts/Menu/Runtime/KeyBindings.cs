using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player key bindings. Every PlayerInput wrapper creates its own copy of the action asset, so each copy
/// registers here: saved overrides (PlayerPrefs, JSON) are applied on registration and pushed to all live
/// copies whenever the player rebinds a key in the Options screen.
/// </summary>
public static class KeyBindings
{
    private const string PrefsKey = "keybindings.v1";

    /// <summary>One rebindable key shown in the Controls tab.</summary>
    public readonly struct Entry
    {
        public readonly string Label;
        public readonly string Action;
        /// <summary>Composite part name ("up"…), or null for a plain binding.</summary>
        public readonly string Part;

        public Entry(string label, string action, string part = null)
        {
            Label = label;
            Action = action;
            Part = part;
        }
    }

    public static readonly Entry[] Rebindable =
    {
        new("Move Up", "Movement", "up"),
        new("Move Down", "Movement", "down"),
        new("Move Left", "Movement", "left"),
        new("Move Right", "Movement", "right"),
        new("Ultimate", "Ultimate"),
        new("Pause", "Pause"),
    };

    private static readonly List<InputActionAsset> Live = new();

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Live.Clear();
        Changed = null;
    }

    public static void Register(InputActionAsset asset)
    {
        if (asset == null || Live.Contains(asset))
            return;

        Live.Add(asset);
        Apply(asset);
    }

    public static void Unregister(InputActionAsset asset) => Live.Remove(asset);

    private static void Apply(InputActionAsset asset)
    {
        asset.RemoveAllBindingOverrides();
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
            asset.LoadBindingOverridesFromJson(json);
    }

    /// <summary>Stores the overrides of <paramref name="source"/> and applies them to every live asset.</summary>
    public static void Save(InputActionAsset source)
    {
        PlayerPrefs.SetString(PrefsKey, source.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        foreach (InputActionAsset asset in Live)
        {
            if (asset != source)
                Apply(asset);
        }

        Changed?.Invoke();
    }

    public static void ResetToDefaults(InputActionAsset source)
    {
        source.RemoveAllBindingOverrides();
        Save(source);
    }

    public static int FindBindingIndex(InputAction action, Entry entry)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (entry.Part == null ? !binding.isComposite && !binding.isPartOfComposite
                                   : binding.isPartOfComposite && string.Equals(binding.name, entry.Part,
                                       StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Short key name for the UI ("W", "Esc", "Space").</summary>
    public static string DisplayName(InputAction action, int bindingIndex)
    {
        if (action == null || bindingIndex < 0)
            return "-";

        string text = action.GetBindingDisplayString(bindingIndex,
            InputBinding.DisplayStringOptions.DontIncludeInteractions);
        return string.IsNullOrEmpty(text) ? "-" : text.ToUpperInvariant();
    }
}
