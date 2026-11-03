#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponData))]
public sealed class WeaponDataEditor : Editor
{
    private readonly Dictionary<int, Editor> nestedEditors = new();

    private void OnDisable()
    {
        foreach (Editor nestedEditor in nestedEditors.Values)
        {
            if (nestedEditor != null)
            {
                DestroyImmediate(nestedEditor);
            }
        }

        nestedEditors.Clear();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        WeaponData weapon = (WeaponData)target;
        DrawLevelOwnedAttackProfiles(weapon);
    }

    private void DrawLevelOwnedAttackProfiles(WeaponData weapon)
    {
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Full Level Attack Profiles", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each level can own a complete independent AttackDefinition. Projectile levels also "
                + "own their ProjectileDefinition, so trajectory, impact, speed, VFX and every "
                + "other attack field can differ between levels.",
            MessageType.Info
        );

        if (weapon.levels == null || weapon.levels.Count == 0)
        {
            return;
        }

        if (GUILayout.Button("Make Every Level Independent", GUILayout.Height(28f)))
        {
            MakeAllLevelsIndependent(weapon);
            GUIUtility.ExitGUI();
        }

        for (int levelIndex = 0; levelIndex < weapon.levels.Count; levelIndex++)
        {
            DrawLevelProfile(weapon, levelIndex);
        }
    }

    private void DrawLevelProfile(WeaponData weapon, int levelIndex)
    {
        WeaponLevelData level = weapon.levels[levelIndex];
        if (level == null)
        {
            return;
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"Level {levelIndex + 1} Attack Profile", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            WeaponAttackDefinition selected = (WeaponAttackDefinition)
                EditorGUILayout.ObjectField(
                    "Attack Profile",
                    level.attack,
                    typeof(WeaponAttackDefinition),
                    false
                );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(weapon, "Change Level Attack Profile");
                level.attack = selected;
                EditorUtility.SetDirty(weapon);
                AssetDatabase.SaveAssets();
            }

            if (level.attack == null)
            {
                if (
                    levelIndex > 0
                    && weapon.levels[levelIndex - 1]?.attack != null
                    && GUILayout.Button("Copy Complete Profile From Previous Level")
                )
                {
                    CloneAttackForLevel(weapon, levelIndex, weapon.levels[levelIndex - 1].attack);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.HelpBox(
                    "Assign an Attack Profile or copy the previous level before editing this level's mechanics.",
                    MessageType.Warning
                );
                return;
            }

            bool ownsAttack = IsOwnedByWeapon(weapon, level.attack);
            if (!ownsAttack)
            {
                EditorGUILayout.HelpBox(
                    "This profile is shared. Editing it also changes every level or weapon that references it.",
                    MessageType.Warning
                );
                if (GUILayout.Button("Make Complete Independent Copy For This Level"))
                {
                    CloneAttackForLevel(weapon, levelIndex, level.attack);
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                EditorGUILayout.LabelField("Ownership", "Owned by this level");
            }

            DrawNestedObject("Complete Attack Configuration", level.attack);

            if (level.attack is ProjectileWeaponAttackDefinition projectileAttack)
            {
                DrawProjectileProfile(weapon, levelIndex, projectileAttack);
            }
        }
    }

    private void DrawProjectileProfile(
        WeaponData weapon,
        int levelIndex,
        ProjectileWeaponAttackDefinition attack
    )
    {
        EditorGUILayout.Space(6f);
        if (attack.projectile == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Projectile Definition in the attack configuration.",
                MessageType.Warning
            );
            return;
        }

        if (!IsOwnedByWeapon(weapon, attack.projectile))
        {
            EditorGUILayout.HelpBox(
                "Projectile Definition is shared. Make it independent before changing trajectory or impact settings.",
                MessageType.Warning
            );
            if (GUILayout.Button("Make Projectile Definition Independent"))
            {
                CloneProjectileForAttack(weapon, levelIndex, attack);
                GUIUtility.ExitGUI();
            }
        }

        DrawNestedObject("Complete Projectile Configuration", attack.projectile);
    }

    private void DrawNestedObject(string title, Object nestedObject)
    {
        if (nestedObject == null)
        {
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        GetNestedEditor(nestedObject).OnInspectorGUI();
        EditorGUI.indentLevel--;
    }

    private Editor GetNestedEditor(Object nestedObject)
    {
        int instanceId = nestedObject.GetInstanceID();
        if (!nestedEditors.TryGetValue(instanceId, out Editor nestedEditor) || nestedEditor == null)
        {
            nestedEditor = CreateEditor(nestedObject);
            nestedEditors[instanceId] = nestedEditor;
        }

        return nestedEditor;
    }

    public static void MakeAllLevelsIndependent(WeaponData weapon)
    {
        if (weapon?.levels == null)
        {
            return;
        }

        for (int levelIndex = 0; levelIndex < weapon.levels.Count; levelIndex++)
        {
            WeaponAttackDefinition source = weapon.levels[levelIndex]?.attack;
            if (source != null && !IsOwnedByWeapon(weapon, source))
            {
                CloneAttackForLevel(weapon, levelIndex, source, saveImmediately: false);
            }
        }

        EditorUtility.SetDirty(weapon);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(weapon), ImportAssetOptions.ForceUpdate);
    }

    public static WeaponAttackDefinition CloneAttackForLevel(
        WeaponData weapon,
        int levelIndex,
        WeaponAttackDefinition source,
        bool saveImmediately = true
    )
    {
        if (
            weapon == null
            || source == null
            || weapon.levels == null
            || levelIndex < 0
            || levelIndex >= weapon.levels.Count
        )
        {
            return null;
        }

        Undo.RecordObject(weapon, "Create Independent Level Attack Profile");
        WeaponAttackDefinition clone = Instantiate(source);
        clone.name = $"{weapon.name}_Level{levelIndex + 1}_{source.GetType().Name}";
        AssetDatabase.AddObjectToAsset(clone, weapon);
        Undo.RegisterCreatedObjectUndo(clone, "Create Independent Level Attack Profile");

        if (clone is ProjectileWeaponAttackDefinition projectileAttack && projectileAttack.projectile != null)
        {
            ProjectileDefinition projectileClone = Instantiate(projectileAttack.projectile);
            projectileClone.name = $"{weapon.name}_Level{levelIndex + 1}_Projectile";
            AssetDatabase.AddObjectToAsset(projectileClone, weapon);
            Undo.RegisterCreatedObjectUndo(projectileClone, "Create Independent Projectile Profile");
            projectileAttack.projectile = projectileClone;
            EditorUtility.SetDirty(projectileAttack);
        }

        weapon.levels[levelIndex].attack = clone;
        EditorUtility.SetDirty(clone);
        EditorUtility.SetDirty(weapon);

        if (saveImmediately)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                AssetDatabase.GetAssetPath(weapon),
                ImportAssetOptions.ForceUpdate
            );
        }

        return clone;
    }

    private static void CloneProjectileForAttack(
        WeaponData weapon,
        int levelIndex,
        ProjectileWeaponAttackDefinition attack
    )
    {
        if (weapon == null || attack == null || attack.projectile == null)
        {
            return;
        }

        Undo.RecordObject(attack, "Create Independent Projectile Profile");
        ProjectileDefinition clone = Instantiate(attack.projectile);
        clone.name = $"{weapon.name}_Level{levelIndex + 1}_Projectile";
        AssetDatabase.AddObjectToAsset(clone, weapon);
        Undo.RegisterCreatedObjectUndo(clone, "Create Independent Projectile Profile");
        attack.projectile = clone;
        EditorUtility.SetDirty(clone);
        EditorUtility.SetDirty(attack);
        EditorUtility.SetDirty(weapon);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(weapon), ImportAssetOptions.ForceUpdate);
    }

    private static bool IsOwnedByWeapon(WeaponData weapon, Object candidate)
    {
        return weapon != null
            && candidate != null
            && AssetDatabase.IsSubAsset(candidate)
            && AssetDatabase.GetAssetPath(candidate) == AssetDatabase.GetAssetPath(weapon);
    }
}
#endif
