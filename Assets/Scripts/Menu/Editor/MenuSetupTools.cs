using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Menu > Setup Menus: imports the Theme Wood sprites as 9-sliced UI sprites, creates the menu theme,
/// the character profiles (data taken from the stage scenes), the stage list and the GameCatalog, builds the
/// MainMenu scene (with a drifting stage in the background), adds a RunController to every stage and puts
/// the scenes in the build. Safe to run again: existing profiles keep their hand-written texts.
/// </summary>
public static class MenuSetupTools
{
    private const string ThemeFolder = "Assets/Theme/Theme Wood";
    private const string MenuFolder = "Assets/GameContent/UI/Menu";
    private const string CatalogPath = "Assets/Resources/GameCatalog.asset";
    private const string MenuScene = "Assets/Scenes/MainMenu.unity";

    private static readonly string[] StageScenes = { "Assets/Scenes/Map_01.unity", "Assets/Scenes/Map_02.unity" };

    // 9-slice borders (left, bottom, right, top) of the Theme Wood sprites, in pixels.
    private static readonly Dictionary<string, Vector4> Borders = new()
    {
        { "nine_path_panel", new Vector4(7, 7, 7, 7) },
        { "nine_path_panel_2", new Vector4(7, 7, 7, 7) },
        { "nine_path_panel_3", new Vector4(7, 7, 7, 7) },
        { "nine_path_panel_disabled", new Vector4(7, 7, 7, 7) },
        { "nine_path_panel_interior", new Vector4(3, 3, 3, 3) },
        { "nine_path_bg", new Vector4(3, 3, 3, 3) },
        { "nine_path_bg_2", new Vector4(3, 3, 3, 3) },
        { "nine_path_focus", new Vector4(3, 3, 3, 3) },
        { "inventory_cell", new Vector4(3, 3, 3, 3) },
        { "button_normal", new Vector4(3, 3, 3, 3) },
        { "button_hover", new Vector4(3, 3, 3, 3) },
        { "button_pressed", new Vector4(3, 3, 3, 3) },
        { "button_disabled", new Vector4(3, 3, 3, 3) },
        { "tab_selected", new Vector4(4, 1, 4, 4) },
        { "tab_unselected", new Vector4(4, 1, 4, 4) },
        { "tab_hover", new Vector4(4, 1, 4, 4) },
        { "slider_progress", new Vector4(4, 4, 4, 4) },
        { "slider_progress_hover", new Vector4(4, 4, 4, 4) },
    };

    private sealed class CharacterText
    {
        public string Epithet, WeaponTitle, WeaponDescription;
        public int Attack, Speed;
    }

    // Card texts written for the menu; ultimate and evolution texts come from the gameplay assets.
    private static readonly Dictionary<string, CharacterText> Texts = new()
    {
        {
            "Lily", new CharacterText
            {
                Epithet = "Hooded Spellcaster", WeaponTitle = "Arcane Burst", Attack = 4, Speed = 3,
                WeaponDescription = "Her floating staff conjures magic explosions right on top of nearby enemies, " +
                                    "hitting everything caught in the blast.",
            }
        },
        {
            "Zena", new CharacterText
            {
                Epithet = "Three-Tailed Fox", WeaponTitle = "Fox Claw / Foxfire", Attack = 4, Speed = 3,
                WeaponDescription = "Melee form rakes enemies with fox claws; Range form shoots foxfire toward the " +
                                    "cursor. Both forms share one weapon level.",
            }
        },
        {
            "Iris", new CharacterText
            {
                Epithet = "Swift Blade", WeaponTitle = "Blade Slash", Attack = 3, Speed = 3,
                WeaponDescription = "Swings a wide slash toward the cursor that cuts through every enemy in the arc.",
            }
        },
    };

    private static readonly (string Scene, string Name, string Description)[] Stages =
    {
        ("Map_01", "Whispering Grove", "Green woods around a forgotten portal. Groves, stone circles and quiet ruins."),
        ("Map_02", "Ancient Ruins", "Autumn ruins between still lakes. Water blocks the way; an altar glows to the north."),
    };

    [MenuItem("Tools/Menu/Setup Menus")]
    public static void SetupAll()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MenuSetup] Stop Play mode first.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(MenuFolder);
        EnsureFolder("Assets/Resources");
        ImportThemeSprites();
        MenuTheme theme = CreateTheme();
        List<CharacterProfile> characters = CreateCharacterProfiles();
        List<StageProfile> stages = CreateStages();

        GameCatalog catalog = LoadOrCreate<GameCatalog>(CatalogPath);
        catalog.theme = theme;
        catalog.characters = characters;
        catalog.stages = stages;
        catalog.mainMenuScene = Path.GetFileNameWithoutExtension(MenuScene);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        foreach (string stage in StageScenes)
            AddRunController(stage);
        CreateMenuScene();
        SetBuildScenes();
        Debug.Log($"[MenuSetup] Done: {characters.Count} character(s), {stages.Count} stage(s), {MenuScene}.");
    }

    // ───────────────────────────── theme ─────────────────────────────

    private static void ImportThemeSprites()
    {
        foreach (string file in Directory.GetFiles(ThemeFolder, "*.png"))
        {
            string path = file.Replace('\\', '/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                continue;

            string name = Path.GetFileNameWithoutExtension(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f; // 1 sprite pixel = 1 canvas unit (canvas reference PPU 100)
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = Borders.TryGetValue(name, out Vector4 border) ? border : Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    private static MenuTheme CreateTheme()
    {
        MenuTheme theme = LoadOrCreate<MenuTheme>($"{MenuFolder}/MenuTheme.asset");
        theme.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/pixel_operator/PixelOperator8.ttf");
        theme.headingFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/pixel_operator/PixelOperator8-Bold.ttf");
        theme.panel = ThemeSprite("nine_path_panel");
        theme.panelInterior = ThemeSprite("nine_path_panel_interior");
        theme.panelDark = ThemeSprite("nine_path_bg");
        theme.panelDarker = ThemeSprite("nine_path_bg_2");
        theme.cell = ThemeSprite("inventory_cell");
        theme.focus = ThemeSprite("nine_path_focus");
        theme.buttonNormal = ThemeSprite("button_normal");
        theme.buttonHover = ThemeSprite("button_hover");
        theme.buttonPressed = ThemeSprite("button_pressed");
        theme.buttonDisabled = ThemeSprite("button_disabled");
        theme.tabSelected = ThemeSprite("tab_selected");
        theme.tabUnselected = ThemeSprite("tab_unselected");
        theme.tabHover = ThemeSprite("tab_hover");
        theme.sliderTrack = ThemeSprite("nine_path_bg_2");
        theme.sliderFill = ThemeSprite("slider_progress");
        theme.sliderHandle = ThemeSprite("h_slidder_grabber");
        theme.toggleOn = ThemeSprite("checked");
        theme.toggleOff = ThemeSprite("unchecked");
        theme.arrowLeft = ThemeSprite("arrow_left");
        theme.arrowRight = ThemeSprite("arrow_right");
        theme.arrowRightHover = ThemeSprite("arrow_right_hover");
        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static Sprite ThemeSprite(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{ThemeFolder}/{name}.png");

    // ───────────────────────────── characters ─────────────────────────────

    private static List<CharacterProfile> CreateCharacterProfiles()
    {
        var result = new List<CharacterProfile>();
        Scene scene = EditorSceneManager.OpenScene(StageScenes[0], OpenSceneMode.Single);
        EnsureFolder($"{MenuFolder}/Characters");

        foreach (string id in new[] { "Lily", "Zena", "Iris" })
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == id && g.GetComponent<PlayerHealth>());
            if (root == null)
            {
                Debug.LogWarning($"[MenuSetup] {id} is not in {StageScenes[0]}.");
                continue;
            }

            string path = $"{MenuFolder}/Characters/{id}.asset";
            bool isNew = !File.Exists(path);
            CharacterProfile profile = LoadOrCreate<CharacterProfile>(path);
            profile.characterId = id;
            profile.displayName = id;
            profile.portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/GameContent/Icon/{id}Icon.png");
            profile.idleFrames = IdleFrames(root);
            profile.previewContent = OpaqueContent(profile.idleFrames);
            profile.maxHealth = root.GetComponent<PlayerHealth>().MaxHealth;

            profile.weapons.Clear();
            profile.ultimate = null;
            profile.formSwitch = null;
            var formSwitch = root.GetComponentInChildren<CharacterFormSwitchController>(true);
            var ultimate = root.GetComponentInChildren<CharacterUltimateController>(true);
            if (formSwitch != null)
            {
                profile.formSwitch = new SerializedObject(formSwitch).FindProperty("data").objectReferenceValue as CharacterFormSwitchData;
                if (profile.formSwitch != null)
                    profile.weapons.AddRange(profile.formSwitch.forms.Where(f => f.weapon != null).Select(f => f.weapon));
            }
            else
            {
                if (ultimate != null)
                    profile.ultimate = new SerializedObject(ultimate).FindProperty("ultimate").objectReferenceValue as UltimateData;
                var weapon = root.GetComponentInChildren<WeaponController>(true);
                if (weapon != null && weapon.Data != null)
                    profile.weapons.Add(weapon.Data);
            }

            if (isNew && Texts.TryGetValue(id, out CharacterText text))
            {
                profile.epithet = text.Epithet;
                profile.weaponTitle = text.WeaponTitle;
                profile.weaponDescription = text.WeaponDescription;
                profile.attackRating = text.Attack;
                profile.speedRating = text.Speed;
            }

            EditorUtility.SetDirty(profile);
            result.Add(profile);
        }

        return result;
    }

    /// <summary>
    /// Normalized rect (inside one frame) covering the opaque pixels of every frame. Read from the PNG file,
    /// so the texture does not need to be readable.
    /// </summary>
    public static Rect OpaqueContent(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null)
            return new Rect(0f, 0f, 1f, 1f);

        var textures = new Dictionary<Texture2D, Texture2D>();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        Rect first = frames[0].rect;

        foreach (Sprite frame in frames)
        {
            if (frame == null)
                continue;

            if (!textures.TryGetValue(frame.texture, out Texture2D pixels))
            {
                pixels = new Texture2D(2, 2);
                pixels.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame.texture)));
                textures[frame.texture] = pixels;
            }

            Rect r = frame.rect;
            for (int y = 0; y < (int)r.height; y++)
            {
                for (int x = 0; x < (int)r.width; x++)
                {
                    if (pixels.GetPixel((int)r.x + x, (int)r.y + y).a <= 0.1f)
                        continue;

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }

        foreach (Texture2D pixels in textures.Values)
            Object.DestroyImmediate(pixels);

        if (maxX < 0)
            return new Rect(0f, 0f, 1f, 1f);

        return Rect.MinMaxRect(minX / first.width, minY / first.height, (maxX + 1) / first.width, (maxY + 1) / first.height);
    }

    /// <summary>Sprite keys of the character's idle clip.</summary>
    private static Sprite[] IdleFrames(GameObject root)
    {
        var animator = root.GetComponentInChildren<Animator>(true);
        AnimationClip clip = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.ToLower().Contains("idle"))
            : null;
        if (clip == null)
        {
            var sr = root.GetComponentInChildren<SpriteRenderer>(true);
            return sr != null && sr.sprite != null ? new[] { sr.sprite } : new Sprite[0];
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
                continue;

            return AnimationUtility.GetObjectReferenceCurve(clip, binding)
                .Select(k => k.value as Sprite).Where(s => s != null).ToArray();
        }

        return new Sprite[0];
    }

    // ───────────────────────────── stages ─────────────────────────────

    private static List<StageProfile> CreateStages()
    {
        EnsureFolder($"{MenuFolder}/Stages");
        var list = new List<StageProfile>();
        foreach ((string scene, string name, string description) in Stages)
        {
            string preview = $"{MenuFolder}/Stages/{scene}_Preview.png";
            if (File.Exists(preview))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(preview);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                // Previews are 1088x608 captures of the running game shown smaller on the card: smooth them.
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            list.Add(new StageProfile
            {
                sceneName = scene,
                displayName = name,
                description = description,
                preview = AssetDatabase.LoadAssetAtPath<Sprite>(preview),
            });
        }

        return list;
    }

    // ───────────────────────────── scenes ─────────────────────────────

    private static void AddRunController(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<RunController>() == null)
        {
            var go = new GameObject("Run Controller");
            go.AddComponent<RunController>();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        EditorSceneManager.SaveScene(scene);
    }

    private static void CreateMenuScene()
    {
        // Background: a copy of the Map_02 stage streaming around a slowly drifting camera.
        Scene stage = EditorSceneManager.OpenScene(StageScenes[1], OpenSceneMode.Single);
        GameObject map = stage.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<MapStreamer>() != null);
        Camera stageCamera = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.CompareTag("MainCamera"));

        Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        if (map != null)
        {
            GameObject copy = Object.Instantiate(map);
            copy.name = map.name;
            var streamer = copy.GetComponent<MapStreamer>();
            var so = new SerializedObject(streamer);
            so.FindProperty("editorPreview").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            SceneManager.MoveGameObjectToScene(copy, menu);
        }

        var cameraGo = new GameObject("Menu Camera") { tag = "MainCamera" };
        var camera = cameraGo.AddComponent<Camera>();
        if (stageCamera != null)
        {
            camera.CopyFrom(stageCamera);
            camera.targetTexture = null;
        }
        else
        {
            camera.orthographic = true;
            camera.orthographicSize = 8f;
        }

        cameraGo.transform.position = new Vector3(0f, 2f, -10f);
        cameraGo.AddComponent<MenuCameraDrift>();
        SceneManager.MoveGameObjectToScene(cameraGo, menu);

        var menuGo = new GameObject("Main Menu");
        menuGo.AddComponent<MainMenuController>();
        SceneManager.MoveGameObjectToScene(menuGo, menu);

        EditorSceneManager.SaveScene(menu, MenuScene);
        EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
    }

    private static void SetBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene> { new(MenuScene, true) };
        scenes.AddRange(StageScenes.Select(s => new EditorBuildSettingsScene(s, true)));
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (scenes.All(s => s.path != existing.path))
                scenes.Add(existing);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ───────────────────────────── helpers ─────────────────────────────

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
