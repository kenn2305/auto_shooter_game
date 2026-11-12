using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Gameplay > Setup Food Spawner: imports every sprite in Assets/Food as a single pixel sprite and
/// (re)creates the "Food Spawner" object in the open scene with those foods. plate.png is an empty dish,
/// not a food, so it is left out.
/// </summary>
public static class FoodSpawnerSetup
{
    private const string FoodFolder = "Assets/Food";
    private const string RootName = "Food Spawner";
    private static readonly HashSet<string> NotFood = new() { "plate" };

    [MenuItem("Tools/Gameplay/Setup Food Spawner")]
    public static void Run()
    {
        List<Sprite> sprites = new();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FoodFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (NotFood.Contains(System.IO.Path.GetFileNameWithoutExtension(path)))
            {
                continue;
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        sprites = sprites.OrderBy(s => s.name).ToList();

        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == RootName)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        GameObject spawnerObject = new(RootName);
        Undo.RegisterCreatedObjectUndo(spawnerObject, "Setup Food Spawner");
        FoodSpawner spawner = spawnerObject.AddComponent<FoodSpawner>();
        SerializedObject so = new(spawner);
        SerializedProperty list = so.FindProperty("foods");
        list.arraySize = sprites.Count;
        for (int i = 0; i < sprites.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        so.FindProperty("blockedLayers").intValue = LayerMask.GetMask("Ostacle");
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(spawnerObject.scene);
        Debug.Log($"[Food] Spawner set up with {sprites.Count} foods: {string.Join(", ", sprites.Select(s => s.name))}");
    }
}
