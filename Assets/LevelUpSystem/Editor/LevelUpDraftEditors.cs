#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelUpDraftController))]
public sealed class LevelUpDraftControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        LevelUpDraftController controller = (LevelUpDraftController)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Level-Up Draft Debug", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Open Draft For Current Level", GUILayout.Height(30f)))
            {
                controller.DebugOpenDraft();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to generate a live draft. Player level does not directly increase Stats; only the selected option is applied.",
                MessageType.Info
            );
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Selection Active", controller.SelectionActive);
            EditorGUILayout.IntField("Pending Level-Ups", controller.PendingLevelUpCount);
            EditorGUILayout.IntField("Selections Made", controller.TotalSelectionsMade);
        }

        IReadOnlyList<LevelUpChoice> choices = controller.CurrentChoices;
        for (int i = 0; i < choices.Count; i++)
        {
            LevelUpChoice choice = choices[i];
            EditorGUILayout.LabelField(
                $"{i + 1}. {choice.Title}",
                $"{choice.Kind} — {choice.Description}"
            );
        }

        Repaint();
    }
}

[CustomEditor(typeof(LevelUpDraftConfig))]
public sealed class LevelUpDraftConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        LevelUpDraftConfig config = (LevelUpDraftConfig)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Configuration Validation", EditorStyles.boldLabel);

        int validStats = 0;
        HashSet<string> ids = new();
        if (config.statPackages != null)
        {
            for (int i = 0; i < config.statPackages.Count; i++)
            {
                LevelUpStatDefinition stat = config.statPackages[i];
                if (stat?.IsConfigured == true && ids.Add(stat.statID))
                {
                    validStats++;
                }
            }
        }

        if (validStats < config.choiceCount)
        {
            EditorGUILayout.HelpBox(
                $"Only {validStats} unique configured Stat packages are available for {config.choiceCount} choices. Runtime safety packages will fill any missing slots.",
                MessageType.Warning
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"{validStats} unique Stat packages can safely fill a complete draft after all equipment is maxed.",
                MessageType.Info
            );
        }

        if (config.maximumWeaponSlots <= 0 && config.weapons?.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "Maximum Weapon Slots is 0, so new weapons can never be acquired.",
                MessageType.Warning
            );
        }
    }
}
#endif
