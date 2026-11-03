#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerExperience))]
public sealed class PlayerExperienceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PlayerExperience experience = (PlayerExperience)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("EXP Runtime Debug", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to inspect live EXP. Player level only emits LevelGained; "
                    + "it does not increase Character Stats.",
                MessageType.Info
            );
            return;
        }

        Rect progressRect = GUILayoutUtility.GetRect(18f, 22f);
        EditorGUI.ProgressBar(
            progressRect,
            experience.NormalizedProgress,
            $"{experience.CurrentExperience} / {experience.ExperienceRequired} EXP "
                + $"({experience.ProgressPercent:0.0}%)"
        );

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Current Level", experience.CurrentLevel);
            EditorGUILayout.IntField("EXP Remaining", experience.ExperienceRemaining);
            EditorGUILayout.IntField("Last EXP Gain", experience.LastExperienceGain);
            EditorGUILayout.IntField(
                "Levels From Last Gain",
                experience.LevelsGainedFromLastGrant
            );
            EditorGUILayout.LongField(
                "Session EXP Collected",
                experience.SessionExperienceCollected
            );
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Debug Grant (Play Mode Only)", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+1 EXP"))
            {
                experience.AddExperience(1);
            }

            if (GUILayout.Button("+10 EXP"))
            {
                experience.AddExperience(10);
            }

            if (GUILayout.Button("+100 EXP"))
            {
                experience.AddExperience(100);
            }
        }

        if (GUI.changed)
        {
            Repaint();
        }
    }
}

[CustomEditor(typeof(ExperienceSystemConfig))]
public sealed class ExperienceSystemConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ExperienceSystemConfig config = (ExperienceSystemConfig)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Drop Chance Summary", EditorStyles.boldLabel);

        float total = config.NormalEnemyConfiguredChanceTotal;
        if (total <= 0f)
        {
            EditorGUILayout.HelpBox(
                "All ratios are zero. Normal EXP is used as a safe 100% fallback so an enemy "
                    + "still always drops one EXP pickup.",
                MessageType.Warning
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Ratio total: {total:0.###}. It is automatically normalized to 100%, "
                    + "so every normal enemy always drops exactly one regular EXP pickup.",
                MessageType.Info
            );
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField(
                "Effective Normal (%)",
                config.GetEffectiveNormalEnemyChancePercent(ExperiencePickupType.Normal)
            );
            EditorGUILayout.FloatField(
                "Effective Green (%)",
                config.GetEffectiveNormalEnemyChancePercent(ExperiencePickupType.Green)
            );
            EditorGUILayout.FloatField(
                "Effective Orange (%)",
                config.GetEffectiveNormalEnemyChancePercent(ExperiencePickupType.Orange)
            );
            EditorGUILayout.FloatField(
                "Rainbow As Fraction",
                config.rainbowDropChancePercent / 100f
            );
        }
    }
}

[CustomEditor(typeof(ExperiencePickupService))]
public sealed class ExperiencePickupServiceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ExperiencePickupService service = (ExperiencePickupService)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Pickup Runtime Debug", EditorStyles.boldLabel);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Runtime counters are available in Play Mode.", MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Active On Map", service.ActivePickupCount);
            EditorGUILayout.FloatField("Current Pickup Radius", service.CurrentPickupRadius);
            EditorGUILayout.LongField("Normal Enemy Rolls", service.NormalEnemyRewardRolls);
            EditorGUILayout.LongField(
                "Normal Spawned",
                service.GetSpawnedPickupCount(ExperiencePickupType.Normal)
            );
            EditorGUILayout.LongField(
                "Green Spawned",
                service.GetSpawnedPickupCount(ExperiencePickupType.Green)
            );
            EditorGUILayout.LongField(
                "Orange Spawned",
                service.GetSpawnedPickupCount(ExperiencePickupType.Orange)
            );
            EditorGUILayout.LongField(
                "Red Spawned",
                service.GetSpawnedPickupCount(ExperiencePickupType.Red)
            );
            EditorGUILayout.LongField(
                "Rainbow Spawned",
                service.GetSpawnedPickupCount(ExperiencePickupType.RainbowMagnet)
            );
        }

        Repaint();
    }
}
#endif
