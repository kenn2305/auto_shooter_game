#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponController))]
public sealed class WeaponControllerEditor : Editor
{
    private SerializedProperty weaponDataProperty;

    private void OnEnable()
    {
        weaponDataProperty = serializedObject.FindProperty("weaponData");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WeaponController controller = (WeaponController)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Debug Level Control", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Debug Level Up", GUILayout.Height(30f)))
            {
                controller.DebugLevelUp();
                Repaint();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Hãy vào Play Mode để dùng nút Debug Level Up. "
                    + "Dữ liệu asset sẽ không bị thay đổi trong Edit Mode.",
                MessageType.Info
            );
            return;
        }

        DrawEvolutionButtons(controller);
        DrawRuntimeValues(controller);
    }

    private static void DrawEvolutionButtons(WeaponController controller)
    {
        if (controller.Data?.evolutions == null || controller.Data.evolutions.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Debug Evolution", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!controller.CanEvolve))
        {
            foreach (WeaponEvolutionData evolution in controller.Data.evolutions)
            {
                if (GUILayout.Button($"Evolve: {evolution.displayName}"))
                {
                    controller.Evolve(evolution);
                }
            }
        }
    }

    private void DrawRuntimeValues(WeaponController controller)
    {
        WeaponData weaponData = weaponDataProperty.objectReferenceValue as WeaponData;
        WeaponLevelData level = controller.CurrentLevel;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Runtime Level Values", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Current Level", controller.DisplayLevel);
            EditorGUILayout.IntField("Maximum Level", controller.MaximumLevel);
            EditorGUILayout.FloatField("Remaining Cooldown", controller.RemainingCooldown);
            EditorGUILayout.TextField(
                "Evolution",
                controller.IsEvolved ? controller.ActiveEvolution.displayName : "None"
            );

            if (weaponData != null)
            {
                EditorGUILayout.FloatField("Base Damage", weaponData.baseDamage);
                EditorGUILayout.FloatField("Attack Interval", weaponData.attackInterval);
                EditorGUILayout.FloatField("Minimum Attack Interval", weaponData.minimumAttackInterval);
                EditorGUILayout.FloatField("Area Multiplier", weaponData.areaMultiplier);
            }

            if (level == null)
            {
                return;
            }

            EditorGUILayout.FloatField(
                "Effective ATK %",
                controller.GetEffectiveStat(CombatStatType.AttackPowerPercent)
            );
            EditorGUILayout.FloatField(
                "Effective Critical %",
                controller.GetEffectiveStat(CombatStatType.CriticalChancePercent)
            );
            EditorGUILayout.FloatField(
                "Effective Attack Speed %",
                controller.GetEffectiveStat(CombatStatType.AttackSpeedPercent)
            );
            EditorGUILayout.FloatField(
                "Owner Movement Speed %",
                controller.GetOwnerStat(CombatStatType.MovementSpeedPercent)
            );
            EditorGUILayout.FloatField(
                "Owner EXP Pickup Range %",
                controller.GetOwnerStat(CombatStatType.ExperiencePickupRangePercent)
            );
            EditorGUILayout.FloatField(
                "Effective Area Multiplier",
                controller.CalculateCurrentAreaMultiplier()
            );
            EditorGUILayout.IntField("Bonuses At This Level", level.statBonuses?.Count ?? 0);
            EditorGUILayout.IntField(
                "Stat Rules At This Level",
                level.statRestrictions?.Count ?? 0
            );
            EditorGUILayout.IntField("Triggered Effects", level.triggeredEffects?.Count ?? 0);
        }
    }
}
#endif
