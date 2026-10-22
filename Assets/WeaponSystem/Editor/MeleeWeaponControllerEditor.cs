#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeleeWeaponController))]
public sealed class MeleeWeaponControllerEditor : Editor
{
    private SerializedProperty weaponDataProperty;

    private void OnEnable()
    {
        weaponDataProperty = serializedObject.FindProperty("weaponData");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeleeWeaponController controller =
            (MeleeWeaponController)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            "Debug Level Control",
            EditorStyles.boldLabel
        );

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button(
                    "Debug Level Up",
                    GUILayout.Height(30f)))
            {
                controller.DebugLevelUp();
                Repaint();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Hãy vào Play Mode để dùng nút Debug Level Up. " +
                "Dữ liệu asset sẽ không bị thay đổi trong Edit Mode.",
                MessageType.Info
            );

            return;
        }

        DrawRuntimeValues(controller);
    }

    private void DrawRuntimeValues(
        MeleeWeaponController controller)
    {
        MeleeWeaponData weaponData =
            weaponDataProperty.objectReferenceValue
                as MeleeWeaponData;

        MeleeWeaponLevelData level =
            controller.CurrentLevel;

        int levelCount =
            weaponData != null &&
            weaponData.levels != null
                ? weaponData.levels.Count
                : 0;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Runtime Level Values",
            EditorStyles.boldLabel
        );

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField(
                "Current Level",
                controller.CurrentLevelIndex + 1
            );

            EditorGUILayout.IntField(
                "Maximum Level",
                levelCount
            );

            EditorGUILayout.Toggle(
                "Is Awakened",
                controller.IsAwakened
            );

            EditorGUILayout.FloatField(
                "Remaining Cooldown",
                controller.RemainingCooldown
            );

            if (weaponData != null)
            {
                EditorGUILayout.FloatField(
                    "Base Damage",
                    weaponData.baseDamage
                );
            }

            if (level != null)
            {
                EditorGUILayout.FloatField(
                    "Level Damage Multiplier",
                    level.damage
                );

                EditorGUILayout.FloatField(
                    "Base Damage At Level",
                    weaponData != null
                        ? weaponData.baseDamage * level.damage
                        : 0f
                );

                EditorGUILayout.FloatField(
                    "Attack Interval",
                    level.attackInterval
                );

                EditorGUILayout.FloatField(
                    "Minimum Attack Interval",
                    level.minimumAttackInterval
                );

                EditorGUILayout.FloatField(
                    "Area Multiplier",
                    level.areaMultiplier
                );
            }
        }
    }
}
#endif
