using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > UI > Setup Player Health Bars: for every character in the open scene, measures the tallest
/// opaque pixel over all of its animation frames and the horizontal body centre, then adds/updates
/// PlayerHealthBar on the character prefab (and the scene instance) so the green bar sits just above
/// the head. Also points CharacterStatsDebugOverlay at the project's pixel font.
/// </summary>
public static class PlayerHealthBarSetup
{
    private const string StatsFontPath = "Assets/pixel_operator/PixelOperator8-Bold.ttf";
    private const int StatsFontSize = 16;

    private struct Measurement
    {
        public float HeadTop;
        public float BodyCenterX;
    }

    [MenuItem("Tools/UI/Setup Player Health Bars")]
    public static void Run()
    {
        Font statsFont = AssetDatabase.LoadAssetAtPath<Font>(StatsFontPath);
        StringBuilder report = new();
        Dictionary<Texture2D, Texture2D> readable = new();

        try
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.GetComponent<PlayerHealth>() == null)
                {
                    continue;
                }

                Measurement m = Measure(root, readable);
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                    Apply(contents, m, statsFont);
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                Apply(root, m, statsFont);
                EditorUtility.SetDirty(root);
                report.AppendLine(
                    $"{root.name}: headTop={m.HeadTop:0.00} bodyCenterX={m.BodyCenterX:0.00} prefab={(string.IsNullOrEmpty(prefabPath) ? "-" : prefabPath)}"
                );
            }
        }
        finally
        {
            foreach (Texture2D copy in readable.Values)
            {
                Object.DestroyImmediate(copy);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[HealthBar] Setup done.\n{report}");
    }

    private static void Apply(GameObject root, Measurement m, Font statsFont)
    {
        PlayerHealthBar bar = root.GetComponent<PlayerHealthBar>();
        if (bar == null)
        {
            bar = root.AddComponent<PlayerHealthBar>();
        }

        SerializedObject barSo = new(bar);
        barSo.FindProperty("headTop").floatValue = m.HeadTop;
        barSo.FindProperty("bodyCenterX").floatValue = m.BodyCenterX;
        barSo.ApplyModifiedPropertiesWithoutUndo();

        CharacterStatsDebugOverlay overlay = root.GetComponent<CharacterStatsDebugOverlay>();
        if (overlay != null)
        {
            SerializedObject overlaySo = new(overlay);
            overlaySo.FindProperty("font").objectReferenceValue = statsFont;
            overlaySo.FindProperty("fontSize").intValue = StatsFontSize;
            overlaySo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Measurement Measure(GameObject root, Dictionary<Texture2D, Texture2D> readable)
    {
        float headTop = float.MinValue;
        float centerSum = 0f;
        int centerCount = 0;

        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            HashSet<Sprite> sprites = CollectSprites(renderer);
            if (sprites.Count == 0)
            {
                continue;
            }

            // Root-space scale of one sprite unit (models are scaled children of the root).
            Vector3 scale = root.transform.InverseTransformVector(renderer.transform.TransformVector(Vector3.one));
            float pivotY = root.transform.InverseTransformPoint(renderer.transform.position).y;

            foreach (Sprite sprite in sprites)
            {
                if (!TryGetOpaqueRect(sprite, readable, out RectInt opaque))
                {
                    continue;
                }

                float ppu = sprite.pixelsPerUnit;
                float top = (opaque.yMax - sprite.pivot.y) / ppu * Mathf.Abs(scale.y);
                headTop = Mathf.Max(headTop, pivotY + top);

                float center = ((opaque.xMin + opaque.xMax) * 0.5f - sprite.pivot.x) / ppu * Mathf.Abs(scale.x);
                centerSum += center;
                centerCount++;
            }
        }

        return new Measurement
        {
            HeadTop = headTop == float.MinValue ? 1.2f : headTop,
            BodyCenterX = centerCount > 0 ? centerSum / centerCount : 0f,
        };
    }

    private static HashSet<Sprite> CollectSprites(SpriteRenderer renderer)
    {
        HashSet<Sprite> sprites = new();
        Animator animator = renderer.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return sprites; // only animated bodies count; weapons and effects are ignored
        }

        if (renderer.sprite != null)
        {
            sprites.Add(renderer.sprite);
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || !string.IsNullOrEmpty(binding.path))
                {
                    continue;
                }

                foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                {
                    if (key.value is Sprite sprite)
                    {
                        sprites.Add(sprite);
                    }
                }
            }
        }

        return sprites;
    }

    /// <summary>Opaque pixel bounds of the sprite, in pixels relative to the sprite rect origin.</summary>
    private static bool TryGetOpaqueRect(Sprite sprite, Dictionary<Texture2D, Texture2D> readable, out RectInt opaque)
    {
        opaque = default;
        Texture2D source = sprite.texture;
        if (!readable.TryGetValue(source, out Texture2D copy))
        {
            string path = AssetDatabase.GetAssetPath(source);
            copy = new Texture2D(2, 2);
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !copy.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(copy);
                copy = null;
            }

            readable[source] = copy;
        }

        if (copy == null)
        {
            return false;
        }

        Rect rect = sprite.rect;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < (int)rect.height; y++)
        {
            for (int x = 0; x < (int)rect.width; x++)
            {
                if (copy.GetPixel((int)rect.x + x, (int)rect.y + y).a > 0.1f)
                {
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x + 1);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y + 1);
                }
            }
        }

        if (maxX < 0)
        {
            return false;
        }

        opaque = new RectInt(minX, minY, maxX - minX, maxY - minY);
        return true;
    }
}
