using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Builds every enemy listed in Assets/GameContent/Enemies/EnemyConfig.csv:
/// moves its sprite sheet into its own folder, slices it per cell, creates the Run animation and
/// controller, writes its EnemyStats asset and builds a prefab from the Enemy.prefab template,
/// sized against the player (Elite x2) with colliders fitted to the sprite.
/// </summary>
public static class EnemyRosterBuilder
{
    private const string EnemiesRoot = "Assets/GameContent/Enemies";
    private const string ConfigPath = EnemiesRoot + "/EnemyConfig.csv";
    private const string TemplatePrefabPath = EnemiesRoot + "/Prefabs/Enemy.prefab";
    private const string RosterPath = EnemiesRoot + "/Data/EnemyRoster.asset";

    private const int PixelsPerUnit = 100;

    // Visible height of the playable characters in world units (Lily 2.5, Iris 2.4, Zena 2.7).
    private const float PlayerVisualSize = 2.6f;
    private const float EliteSizeMultiplier = 2f;

    // Collider = this fraction of the sprite body (same ratio as the player's 1.49 x 2.11 capsule).
    private const float BodyColliderWidthRatio = 0.8f;
    private const float BodyColliderHeightRatio = 0.85f;
    private const float DamageTriggerRatio = 1.1f;
    private const float SeparationRadiusRatio = 0.6f;

    private const byte AlphaThreshold = 8;

    private sealed class EnemyRow
    {
        public string Name;
        public EnemyExperienceRank Rank;
        public int HP;
        public int ATK;
        public float AttackInterval;
        public float SPD;
        public int EXP;
        public float Size;
        public string Sprite;
        public int Frames;
        public int CellW;
        public int CellH;
        public float FPS;
        public bool FacesLeft;
        public bool Stationary;
        public float Weight;
        public int MaxAlive;

        public string Folder =>
            $"{EnemiesRoot}/{(Rank == EnemyExperienceRank.Normal ? "NormalEnemy" : "EliteEnemy")}/{Name}";
    }

    private struct BodyMetrics
    {
        public Vector2 PivotNormalized;
        public float ReferencePixels;
        public Vector2 MedianFramePixels;
    }

    [MenuItem("Tools/Enemies/Build From EnemyConfig.csv")]
    public static void BuildAll()
    {
        List<EnemyRow> rows = ReadConfig();
        if (rows == null)
            return;

        StringBuilder report = new StringBuilder("[EnemyRoster] Built enemies:\n");

        try
        {
            for (int i = 0; i < rows.Count; i++)
            {
                EnemyRow row = rows[i];
                EditorUtility.DisplayProgressBar("Building enemies", row.Name, (float)i / rows.Count);
                report.AppendLine(BuildEnemy(row, rows));
            }

            WriteRoster(rows);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Enemies/Apply Stats Only")]
    public static void ApplyStatsOnly()
    {
        List<EnemyRow> rows = ReadConfig();
        if (rows == null)
            return;

        foreach (EnemyRow row in rows)
        {
            EnsureFolder(row.Folder + "/Data");
            WriteStats(row);
        }

        WriteRoster(rows);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyRoster] Stats applied for {rows.Count} enemies.");
    }

    // ---------------------------------------------------------------- config

    private static List<EnemyRow> ReadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            Debug.LogError($"[EnemyRoster] Missing {ConfigPath}");
            return null;
        }

        List<EnemyRow> rows = new List<EnemyRow>();
        string[] header = null;
        string[] lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            string[] cells = line.Split(',').Select(c => c.Trim()).ToArray();
            if (cells.All(c => c.Length == 0))
                continue;

            if (header == null)
            {
                header = cells;
                continue;
            }

            try
            {
                rows.Add(ParseRow(header, cells));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EnemyRoster] {ConfigPath} line {lineIndex + 1}: {exception.Message}");
                return null;
            }
        }

        HashSet<string> names = new HashSet<string>();
        foreach (EnemyRow row in rows)
        {
            if (!names.Add(row.Name))
            {
                Debug.LogError($"[EnemyRoster] Duplicate enemy name '{row.Name}'.");
                return null;
            }
        }

        return rows;
    }

    private static EnemyRow ParseRow(string[] header, string[] cells)
    {
        string Get(string column)
        {
            int index = Array.FindIndex(header, h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                throw new FormatException($"column '{column}' is missing from the header");
            if (index >= cells.Length || cells[index].Length == 0)
                throw new FormatException($"'{column}' is empty");
            return cells[index];
        }

        // Optional columns fall back to a default so older config files keep working.
        string Optional(string column, string fallback)
        {
            int index = Array.FindIndex(header, h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index < cells.Length && cells[index].Length > 0 ? cells[index] : fallback;
        }

        bool Yes(string value) => value.ToLowerInvariant() is "yes" or "y" or "true" or "1" or "x";

        int Int(string column) => int.Parse(Get(column), CultureInfo.InvariantCulture);
        float Float(string column) => float.Parse(Get(column), CultureInfo.InvariantCulture);

        string rank = Get("Rank");
        if (!Enum.TryParse(rank, true, out EnemyExperienceRank parsedRank))
            throw new FormatException($"Rank '{rank}' must be Normal or Elite");

        string facing = Get("FacesLeft").ToLowerInvariant();

        return new EnemyRow
        {
            Name = Get("Name"),
            Rank = parsedRank,
            HP = Int("HP"),
            ATK = Int("ATK"),
            AttackInterval = Float("AttackInterval"),
            SPD = Float("SPD"),
            EXP = Int("EXP"),
            Size = Float("Size"),
            Sprite = Get("Sprite"),
            Frames = Int("Frames"),
            CellW = Int("CellW"),
            CellH = Int("CellH"),
            FPS = Float("FPS"),
            FacesLeft = Yes(facing),
            Stationary = Yes(Optional("Stationary", "no")),
            Weight = float.Parse(Optional("Weight", "1"), CultureInfo.InvariantCulture),
            MaxAlive = int.Parse(Optional("MaxAlive", "0"), CultureInfo.InvariantCulture),
        };
    }

    // ---------------------------------------------------------------- build

    private static string BuildEnemy(EnemyRow row, List<EnemyRow> allRows)
    {
        EnsureFolder(row.Folder + "/Sprites");
        EnsureFolder(row.Folder + "/Animation");
        EnsureFolder(row.Folder + "/Data");

        string texturePath = PlaceTexture(row, allRows);
        BodyMetrics metrics = MeasureBody(texturePath, row);
        Sprite[] frames = SliceTexture(texturePath, row, metrics.PivotNormalized);

        AnimationClip clip = WriteRunClip(row, frames);
        AnimatorController controller = WriteController(row, clip);
        EnemyStats stats = WriteStats(row);

        float worldSize = PlayerVisualSize * row.Size * (row.Rank == EnemyExperienceRank.Normal ? 1f : EliteSizeMultiplier);
        float modelScale = worldSize * PixelsPerUnit / metrics.ReferencePixels;
        Vector2 body = metrics.MedianFramePixels * (modelScale / PixelsPerUnit);
        Vector2 colliderSize = new Vector2(body.x * BodyColliderWidthRatio, body.y * BodyColliderHeightRatio);

        WritePrefab(row, frames[0], controller, stats, modelScale, colliderSize);

        return $"  {row.Name,-18} {row.Rank,-6} frames {row.Frames} @ {row.CellW}x{row.CellH}  "
            + $"scale {modelScale:0.##}  collider {colliderSize.x:0.##} x {colliderSize.y:0.##}";
    }

    private static string PlaceTexture(EnemyRow row, List<EnemyRow> allRows)
    {
        string targetPath = $"{row.Folder}/Sprites/{row.Name}.png";
        if (File.Exists(targetPath))
            return targetPath;

        string sourcePath = $"{EnemiesRoot}/{row.Sprite}";
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"[EnemyRoster] {row.Name}: sprite sheet not found at {sourcePath}");

        bool shared = allRows.Count(r => r != row && r.Sprite == row.Sprite && !File.Exists($"{r.Folder}/Sprites/{r.Name}.png")) > 0;
        bool ok = shared ? AssetDatabase.CopyAsset(sourcePath, targetPath) : string.IsNullOrEmpty(AssetDatabase.MoveAsset(sourcePath, targetPath));
        if (!ok)
            throw new IOException($"[EnemyRoster] {row.Name}: could not place {sourcePath} at {targetPath}");

        return targetPath;
    }

    private static BodyMetrics MeasureBody(string texturePath, EnemyRow row)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            texture.LoadImage(File.ReadAllBytes(texturePath));
            ValidateSheet(row, texture.width, texture.height);

            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int top = texture.height - row.CellH;

            int unionMinX = int.MaxValue, unionMinY = int.MaxValue, unionMaxX = int.MinValue, unionMaxY = int.MinValue;
            List<int> frameWidths = new List<int>();
            List<int> frameHeights = new List<int>();

            for (int frame = 0; frame < row.Frames; frame++)
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

                for (int y = 0; y < row.CellH; y++)
                {
                    for (int x = 0; x < row.CellW; x++)
                    {
                        if (pixels[(top + y) * width + frame * row.CellW + x].a <= AlphaThreshold)
                            continue;

                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                if (maxX < minX)
                    continue;

                frameWidths.Add(maxX - minX + 1);
                frameHeights.Add(maxY - minY + 1);
                unionMinX = Mathf.Min(unionMinX, minX);
                unionMaxX = Mathf.Max(unionMaxX, maxX);
                unionMinY = Mathf.Min(unionMinY, minY);
                unionMaxY = Mathf.Max(unionMaxY, maxY);
            }

            if (frameWidths.Count == 0)
                throw new InvalidDataException($"[EnemyRoster] {row.Name}: sprite sheet is empty");

            frameWidths.Sort();
            frameHeights.Sort();

            float centerX = (unionMinX + unionMaxX + 1) * 0.5f;
            float centerY = (unionMinY + unionMaxY + 1) * 0.5f;

            return new BodyMetrics
            {
                // Pivot on the body centre so flipping and colliders stay centred on the creature.
                PivotNormalized = new Vector2(centerX / row.CellW, centerY / row.CellH),
                ReferencePixels = Mathf.Max(unionMaxX - unionMinX + 1, unionMaxY - unionMinY + 1),
                MedianFramePixels = new Vector2(frameWidths[frameWidths.Count / 2], frameHeights[frameHeights.Count / 2]),
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ValidateSheet(EnemyRow row, int width, int height)
    {
        if (row.Frames * row.CellW > width || row.CellH > height)
        {
            throw new InvalidDataException(
                $"[EnemyRoster] {row.Name}: {row.Frames} x {row.CellW}x{row.CellH} does not fit the {width}x{height} sheet"
            );
        }
    }

    private static Sprite[] SliceTexture(string texturePath, EnemyRow row, Vector2 pivot)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        ValidateSheet(row, width, height);

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Dictionary<string, SpriteRect> existing = provider.GetSpriteRects().GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First());
        SpriteRect[] rects = new SpriteRect[row.Frames];

        for (int i = 0; i < row.Frames; i++)
        {
            string spriteName = $"{row.Name}_{i}";
            SpriteRect rect = existing.TryGetValue(spriteName, out SpriteRect found) ? found : new SpriteRect { spriteID = GUID.Generate() };
            rect.name = spriteName;
            rect.rect = new Rect(i * row.CellW, height - row.CellH, row.CellW, row.CellH);
            rect.alignment = SpriteAlignment.Custom;
            rect.pivot = pivot;
            rect.border = Vector4.zero;
            rects[i] = rect;
        }

        provider.SetSpriteRects(rects);

        ISpriteNameFileIdDataProvider nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameProvider?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

        provider.Apply();
        importer.SaveAndReimport();

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath)
            .OfType<Sprite>()
            .Where(s => s.name.StartsWith(row.Name + "_"))
            .OrderBy(s => int.Parse(s.name.Substring(row.Name.Length + 1), CultureInfo.InvariantCulture))
            .ToArray();

        if (sprites.Length != row.Frames)
            throw new InvalidDataException($"[EnemyRoster] {row.Name}: expected {row.Frames} sprites, got {sprites.Length}");

        return sprites;
    }

    private static AnimationClip WriteRunClip(EnemyRow row, Sprite[] frames)
    {
        string path = $"{row.Folder}/Animation/Run.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = clip == null;
        if (isNew)
            clip = new AnimationClip { name = "Run" };

        clip.frameRate = row.FPS;

        ObjectReferenceKeyframe[] keys = frames
            .Select((sprite, index) => new ObjectReferenceKeyframe { time = index / row.FPS, value = sprite })
            .ToArray();

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew)
            AssetDatabase.CreateAsset(clip, path);
        else
            EditorUtility.SetDirty(clip);

        return clip;
    }

    private static AnimatorController WriteController(EnemyRow row, AnimationClip clip)
    {
        string path = $"{row.Folder}/Animation/{row.Name.ToUpperInvariant()}_ANIM.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

        if (controller == null)
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState run = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Run") ?? machine.AddState("Run");
        run.motion = clip;
        machine.defaultState = run;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static EnemyStats WriteStats(EnemyRow row)
    {
        string path = $"{row.Folder}/Data/{row.Name}Stat.asset";
        EnemyStats stats = AssetDatabase.LoadAssetAtPath<EnemyStats>(path);
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<EnemyStats>();
            AssetDatabase.CreateAsset(stats, path);
        }

        stats.Rank = row.Rank;
        stats.HP = Mathf.Max(1, row.HP);
        stats.ATK = Mathf.Max(1, row.ATK);
        stats.AttackInterval = Mathf.Max(0.05f, row.AttackInterval);
        stats.SPD = Mathf.Max(0f, row.SPD);
        stats.EXP = Mathf.Max(1, row.EXP);
        stats.Stationary = row.Stationary;
        stats.SpawnWeight = Mathf.Max(0f, row.Weight);
        stats.MaxAlive = Mathf.Max(0, row.MaxAlive);
        EditorUtility.SetDirty(stats);
        return stats;
    }

    private static void WritePrefab(
        EnemyRow row,
        Sprite firstFrame,
        RuntimeAnimatorController controller,
        EnemyStats stats,
        float modelScale,
        Vector2 colliderSize
    )
    {
        string prefabPath = $"{row.Folder}/{row.Name}.prefab";
        bool exists = File.Exists(prefabPath);
        GameObject root = PrefabUtility.LoadPrefabContents(exists ? prefabPath : TemplatePrefabPath);

        try
        {
            root.name = row.Name;

            // ---- Model (sprite + animator), named like the player's.
            Transform model = root.transform.Find("Model");
            if (model == null)
                model = root.transform.Find("Capsule");
            if (model == null)
                throw new InvalidDataException($"[EnemyRoster] {TemplatePrefabPath} has no Model/Capsule child");

            model.name = "Model";
            model.localPosition = Vector3.zero;
            model.localScale = new Vector3(modelScale, modelScale, modelScale);

            SpriteRenderer renderer = model.GetComponent<SpriteRenderer>();
            renderer.sprite = firstFrame;
            renderer.flipX = false;

            Animator animator = GetOrAdd<Animator>(model.gameObject);
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;

            // ---- Body collider.
            CapsuleDirection2D direction = colliderSize.y >= colliderSize.x ? CapsuleDirection2D.Vertical : CapsuleDirection2D.Horizontal;
            CapsuleCollider2D bodyCollider = root.GetComponent<CapsuleCollider2D>();
            bodyCollider.offset = Vector2.zero;
            bodyCollider.size = colliderSize;
            bodyCollider.direction = direction;

            // Stationary enemies are immovable: kinematic bodies still block and trigger the player.
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            body.bodyType = row.Stationary ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;

            // ---- Contact damage trigger.
            Transform trigger = root.transform.Find("DamageTrigger");
            if (trigger != null)
            {
                trigger.localPosition = Vector3.zero;
                CapsuleCollider2D triggerCollider = trigger.GetComponent<CapsuleCollider2D>();
                triggerCollider.offset = Vector2.zero;
                triggerCollider.size = colliderSize * DamageTriggerRatio;
                triggerCollider.direction = direction;

                SetSerialized(trigger.GetComponent<EnemyContactDamage>(), so =>
                {
                    so.FindProperty("damagePerHit").intValue = stats.ATK;
                    so.FindProperty("damageInterval").floatValue = stats.AttackInterval;
                });
            }

            // ---- Mirror stats in the inspector (EnemyStatsBinder re-applies them at runtime).
            SetSerialized(root.GetComponent<EnemyHealth>(), so =>
            {
                so.FindProperty("maxHealth").intValue = stats.HP;
                // Per-hit logging floods the console once hundreds of enemies are alive.
                SerializedProperty log = so.FindProperty("logAcceptedHits");
                if (log != null)
                    log.boolValue = false;
            });
            SetSerialized(root.GetComponent<EnemyFlowFieldAgent>(), so =>
            {
                so.FindProperty("moveSpeed").floatValue = stats.SPD;
                so.FindProperty("separationRadius").floatValue = Mathf.Max(colliderSize.x, colliderSize.y) * SeparationRadiusRatio;
            });
            SetSerialized(root.GetComponent<EnemyExperienceDropper>(), so =>
            {
                so.FindProperty("rank").enumValueIndex = (int)stats.Rank;
                so.FindProperty("baseExperience").intValue = stats.EXP;
            });

            EnemyStatsBinder binder = GetOrAdd<EnemyStatsBinder>(root);
            SetSerialized(binder, so => so.FindProperty("stats").objectReferenceValue = stats);

            EnemyVisual visual = GetOrAdd<EnemyVisual>(root);
            SetSerialized(visual, so =>
            {
                so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
                so.FindProperty("animator").objectReferenceValue = animator;
                so.FindProperty("spriteFacesLeft").boolValue = row.FacesLeft;
            });

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
            if (!success)
                throw new IOException($"[EnemyRoster] {row.Name}: could not save {prefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WriteRoster(List<EnemyRow> rows)
    {
        EnsureFolder(EnemiesRoot + "/Data");
        EnemyRoster roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>(RosterPath);
        if (roster == null)
        {
            roster = ScriptableObject.CreateInstance<EnemyRoster>();
            AssetDatabase.CreateAsset(roster, RosterPath);
        }

        roster.normalEnemies.Clear();
        roster.eliteEnemies.Clear();

        foreach (EnemyRow row in rows)
        {
            EnemyPoolMember prefab = AssetDatabase.LoadAssetAtPath<EnemyPoolMember>($"{row.Folder}/{row.Name}.prefab");
            if (prefab == null)
                continue;

            (row.Rank == EnemyExperienceRank.Normal ? roster.normalEnemies : roster.eliteEnemies).Add(prefab);
        }

        EditorUtility.SetDirty(roster);
    }

    // ---------------------------------------------------------------- helpers

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        return target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();
    }

    private static void SetSerialized(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        if (target == null)
            return;

        SerializedObject serialized = new SerializedObject(target);
        edit(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
