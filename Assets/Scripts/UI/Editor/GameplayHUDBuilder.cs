using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > UI > Build Gameplay HUD: generates the HUD pixel sprites and (re)builds the
/// "Gameplay HUD" canvas in the open scene. Layout is in 1080p units (scale snapped by
/// PixelPerfectCanvasScale); pixel art is shown at 2x (portrait, skull, frames) and 1x (slot icons).
/// </summary>
public static class GameplayHUDBuilder
{
    private const string RootName = "Gameplay HUD";
    private const string SpriteFolder = "Assets/GameContent/UI/HUD";
    private const string IconFolder = "Assets/GameContent/Icon";
    // PixelOperator8 is drawn on an 8 px grid: with font sizes that are multiples of 16 and the canvas
    // scale snapped to 0.5 steps (PixelPerfectCanvasScale), glyphs always land on whole screen pixels.
    private const string FontPath = "Assets/pixel_operator/PixelOperator8-Bold.ttf";
    private const string SmallFontPath = "Assets/pixel_operator/PixelOperator8-Bold.ttf";

    // Layout (reference units).
    // Pixel sprites use 2 units per sprite pixel (PixelScale) so they stay whole pixels at every 0.5 canvas step.
    private const float PixelScale = 2f;
    // Character cluster (portrait, slots, skull) is drawn 1.25x larger than the base design; the EXP bar is not.
    private const int PortraitSpritePixels = 55; // 1 ink + 2 ring + 1 ink + inner, per side (44 px base x 1.25)
    private const float Margin = 16f;
    private const float ExpBarHeight = 24f;
    private const float ClusterTop = 36f;
    private const float PortraitSize = PortraitSpritePixels * PixelScale; // 110
    private const float PortraitIconSize = 80f; // 32 px icon at 2.5x
    private const float SlotSize = 50f;
    private const float SlotIconSize = 40f; // 32 px icon at 1.25x
    private const float SkullIconSize = 40f; // 16 px skull at 2.5x
    private const float SlotSpacing = 10f; // 2 rows: 50 + 10 + 50 = 110 = portrait height
    private const float LevelTabWidth = 192f; // "LV 99" at font 32 with padding
    private const float LevelTabHeight = 56f; // hangs below the 24-unit EXP bar
    private const float ClusterGap = SlotSpacing; // portrait → first slot uses the same even spacing
    private const int WeaponSlotCount = 3;
    private const int ItemSlotCount = 3;

    private static readonly Color Ink = new(0.04f, 0.04f, 0.07f, 1f);
    private static readonly Color PanelDark = new(0.08f, 0.09f, 0.17f, 1f);
    private static readonly Color ExpFrame = new(0.11f, 0.13f, 0.24f, 1f);
    private static readonly Color ExpFill = new(0.42f, 0.64f, 1f, 1f);
    private static readonly Color MainSlotTint = new(0.95f, 0.38f, 0.4f, 1f);
    private static readonly Color WeaponSlotTint = new(0.62f, 0.72f, 0.82f, 1f);
    private static readonly Color ItemSlotTint = new(0.55f, 0.9f, 0.38f, 1f);

    private sealed class HUDSprites
    {
        public Sprite Pixel;
        public Sprite Panel;
        public Sprite BarFill;
        public Sprite SlotFrame;
        public Sprite PortraitBack;
        public Sprite Ring;
    }

    [MenuItem("Tools/UI/Build Gameplay HUD")]
    public static void BuildInOpenScene()
    {
        HUDSprites sprites = GenerateSprites();
        UsePointFilter(IconFolder);

        UseRasterFont(FontPath);
        UseRasterFont(SmallFontPath);
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        Font smallFont = AssetDatabase.LoadAssetAtPath<Font>(SmallFontPath);
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        smallFont ??= font;

        // The blank slot icons were auto-sliced into corner pieces; the HUD needs the whole 32x32 image.
        UseSingleSprite($"{IconFolder}/BlankWeaponSlotIcon.png");
        UseSingleSprite($"{IconFolder}/BlankWeaponSlotIcon 1.png");
        Sprite weaponBlank = LoadSprite($"{IconFolder}/BlankWeaponSlotIcon.png");
        Sprite itemBlank = LoadSprite($"{IconFolder}/BlankWeaponSlotIcon 1.png");
        Sprite skull = LoadSprite($"{IconFolder}/SkullDeadIcon.png");

        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == RootName)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        GameObject hudObject = new(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(hudObject, "Build Gameplay HUD");
        hudObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = hudObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // under the level-up draft (1000)
        canvas.pixelPerfect = true;
        CanvasScaler scaler = hudObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        hudObject.AddComponent<PixelPerfectCanvasScale>();

        GameplayHUD hud = hudObject.AddComponent<GameplayHUD>();
        SerializedObject so = new(hud);

        // ---------------- EXP bar + level tab (top, full width)
        RectTransform expBar = CreateRect("EXP Bar", hudObject.transform);
        expBar.anchorMin = new Vector2(0f, 1f);
        expBar.anchorMax = new Vector2(1f, 1f);
        expBar.pivot = new Vector2(0.5f, 1f);
        expBar.sizeDelta = new Vector2(0f, ExpBarHeight);
        expBar.anchoredPosition = Vector2.zero;
        AddImage(expBar, sprites.Panel, ExpFrame, Image.Type.Sliced);

        RectTransform expFillRect = CreateRect("Fill", expBar);
        Stretch(expFillRect, 4f);
        Image expFill = AddImage(expFillRect, sprites.BarFill, ExpFill, Image.Type.Filled);
        expFill.fillMethod = Image.FillMethod.Horizontal;
        expFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        expFill.fillAmount = 0.35f;

        RectTransform levelTab = CreateRect("Level Tab", expBar);
        Anchor(levelTab, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(LevelTabWidth, LevelTabHeight));
        AddImage(levelTab, sprites.Panel, PanelDark, Image.Type.Sliced);
        RectTransform levelTextRect = CreateRect("Level Text", levelTab);
        Stretch(levelTextRect, 0f);
        Text levelText = AddText(levelTextRect, font, 32, TextAnchor.MiddleCenter, Color.white, "LV 1");

        // ---------------- Timer (top centre, level with the weapon row)
        RectTransform timer = CreateRect("Timer", hudObject.transform);
        Anchor(timer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -ClusterTop), new Vector2(200f, SlotSize));
        Text timerText = AddText(timer, font, 32, TextAnchor.MiddleCenter, Color.white, "00:00");

        // ---------------- Kill counter (top right): skull + number, money removed
        RectTransform kills = CreateRect("Kills", hudObject.transform);
        // Starts under the (taller) level tab so the two never overlap.
        Anchor(
            kills,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-Margin, -(LevelTabHeight + SlotSpacing)),
            new Vector2(200f, SlotSize)
        );
        HorizontalLayoutGroup killLayout = kills.gameObject.AddComponent<HorizontalLayoutGroup>();
        killLayout.spacing = SlotSpacing;
        killLayout.childAlignment = TextAnchor.MiddleRight;
        killLayout.childControlWidth = true;
        killLayout.childControlHeight = true;
        killLayout.childForceExpandWidth = false;
        killLayout.childForceExpandHeight = false;

        RectTransform skullRect = CreateRect("Skull Icon", kills);
        Image skullImage = AddImage(skullRect, skull, Color.white, Image.Type.Simple);
        skullImage.preserveAspect = true;
        LayoutElement skullLayout = skullRect.gameObject.AddComponent<LayoutElement>();
        skullLayout.minWidth = skullLayout.preferredWidth = SkullIconSize;
        skullLayout.minHeight = skullLayout.preferredHeight = SkullIconSize;

        RectTransform killTextRect = CreateRect("Kill Text", kills);
        Text killText = AddText(killTextRect, font, 32, TextAnchor.MiddleRight, Color.white, "0");
        killText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // ---------------- Portrait with Ultimate cooldown border (top left)
        RectTransform portrait = CreateRect("Portrait", hudObject.transform);
        Anchor(
            portrait,
            new Vector2(0f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(Margin + PortraitSize * 0.5f, -(ClusterTop + PortraitSize * 0.5f)),
            new Vector2(PortraitSize, PortraitSize)
        );
        RectTransform portraitBack = CreateRect("Back", portrait);
        Stretch(portraitBack, 0f);
        AddImage(portraitBack, sprites.PortraitBack, Color.white, Image.Type.Simple);

        RectTransform portraitIconRect = CreateRect("Character Icon", portrait);
        Stretch(portraitIconRect, (PortraitSize - PortraitIconSize) * 0.5f);
        Image portraitIcon = AddImage(portraitIconRect, LoadSprite($"{IconFolder}/ZenaIcon.png"), Color.white, Image.Type.Simple);
        portraitIcon.preserveAspect = true;

        RectTransform ringRect = CreateRect("Ultimate Ring", portrait);
        Stretch(ringRect, 0f);
        Image ring = AddImage(ringRect, sprites.Ring, new Color(1f, 0.82f, 0.25f, 1f), Image.Type.Filled);
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = true;
        ring.fillAmount = 0.6f;

        RectTransform cooldownRect = CreateRect("Cooldown Text", portrait);
        Anchor(cooldownRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(PortraitIconSize, 20f));
        Text cooldownText = AddText(cooldownRect, font, 16, TextAnchor.MiddleCenter, Color.white, "");

        // ---------------- Slots: row 1 = main weapon + weapons, row 2 = items (evenly spaced grid)
        float slotsLeft = Margin + PortraitSize + ClusterGap;
        RectTransform slots = CreateRect("Slots", hudObject.transform);
        Anchor(
            slots,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(slotsLeft, -ClusterTop),
            new Vector2((WeaponSlotCount + 1) * SlotSize + WeaponSlotCount * SlotSpacing, SlotSize * 2f + SlotSpacing)
        );

        HUDSlotView mainSlot = CreateSlot(slots, "Main Weapon", 0, 0, MainSlotTint, weaponBlank, sprites, smallFont);
        HUDSlotView[] weaponSlots = new HUDSlotView[WeaponSlotCount];
        for (int i = 0; i < WeaponSlotCount; i++)
        {
            weaponSlots[i] = CreateSlot(slots, $"Weapon {i + 1}", i + 1, 0, WeaponSlotTint, weaponBlank, sprites, smallFont);
        }

        HUDSlotView[] itemSlots = new HUDSlotView[ItemSlotCount];
        for (int i = 0; i < ItemSlotCount; i++)
        {
            itemSlots[i] = CreateSlot(slots, $"Item {i + 1}", i, 1, ItemSlotTint, itemBlank, sprites, smallFont);
        }

        // ---------------- Wire the HUD component
        SerializedProperty portraitList = so.FindProperty("portraits");
        portraitList.ClearArray();
        string[] characters = { "Iris", "Lily", "Zena" };
        for (int i = 0; i < characters.Length; i++)
        {
            portraitList.InsertArrayElementAtIndex(i);
            SerializedProperty entry = portraitList.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("characterName").stringValue = characters[i];
            entry.FindPropertyRelative("portrait").objectReferenceValue = LoadSprite($"{IconFolder}/{characters[i]}Icon.png");
        }

        so.FindProperty("experienceFill").objectReferenceValue = expFill;
        so.FindProperty("levelText").objectReferenceValue = levelText;
        so.FindProperty("timerText").objectReferenceValue = timerText;
        so.FindProperty("killText").objectReferenceValue = killText;
        so.FindProperty("killPunchTarget").objectReferenceValue = skullRect;
        so.FindProperty("portraitImage").objectReferenceValue = portraitIcon;
        so.FindProperty("portraitRoot").objectReferenceValue = portrait;
        so.FindProperty("ultimateRing").objectReferenceValue = ring;
        so.FindProperty("ultimateCooldownText").objectReferenceValue = cooldownText;
        so.FindProperty("mainWeaponSlot").objectReferenceValue = mainSlot;
        AssignArray(so.FindProperty("weaponSlots"), weaponSlots);
        AssignArray(so.FindProperty("itemSlots"), itemSlots);
        so.ApplyModifiedPropertiesWithoutUndo();

        BuildLevelUpDraftUI(font, sprites.Panel);
        BuildFloatingText(font);

        Selection.activeGameObject = hudObject;
        EditorSceneManager.MarkSceneDirty(hudObject.scene);
        Debug.Log($"[HUD] Built '{RootName}' in scene '{hudObject.scene.name}'. Save the scene to keep it.", hudObject);
    }

    // ------------------------------------------------------------------ Level-up draft

    private const string DraftRootName = "Level Up Draft UI";
    private const string UpgradeLabelPath = IconFolder + "/ChoseUpgradeLabel.aseprite";

    /// <summary>
    /// Scene holder for the level-up screen: LevelUpDraftController finds it (inactive) and the screen
    /// builds itself on first Show with this font, the ChoseUpgradeLabel panel and the HUD name plate.
    /// </summary>
    private static void BuildLevelUpDraftUI(Font font, Sprite plate)
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == DraftRootName)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        GameObject draftObject = new(DraftRootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(draftObject, "Build Level Up Draft UI");
        draftObject.layer = LayerMask.NameToLayer("UI");
        LevelUpDraftUI draft = draftObject.AddComponent<LevelUpDraftUI>();

        SerializedObject draftSo = new(draft);
        draftSo.FindProperty("font").objectReferenceValue = font;
        draftSo.FindProperty("panelSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(UpgradeLabelPath);
        draftSo.FindProperty("plateSprite").objectReferenceValue = plate;
        draftSo.ApplyModifiedPropertiesWithoutUndo();
        draftObject.SetActive(false);
    }

    /// <summary>Scene holder for damage / heal numbers, carrying the pixel font.</summary>
    private static void BuildFloatingText(Font font)
    {
        const string name = "Floating Text";
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        GameObject textObject = new(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(textObject, "Build Floating Text");
        textObject.layer = LayerMask.NameToLayer("UI");
        FloatingTextService service = textObject.AddComponent<FloatingTextService>();
        SerializedObject serviceSo = new(service);
        serviceSo.FindProperty("font").objectReferenceValue = font;
        serviceSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Slots

    private static HUDSlotView CreateSlot(
        RectTransform parent,
        string name,
        int column,
        int row,
        Color tint,
        Sprite placeholder,
        HUDSprites sprites,
        Font badgeFont
    )
    {
        RectTransform slot = CreateRect(name, parent);
        Anchor(
            slot,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(column * (SlotSize + SlotSpacing), -row * (SlotSize + SlotSpacing)),
            new Vector2(SlotSize, SlotSize)
        );
        Image frame = AddImage(slot, sprites.SlotFrame, tint, Image.Type.Sliced);

        RectTransform iconRect = CreateRect("Icon", slot);
        Stretch(iconRect, (SlotSize - SlotIconSize) * 0.5f);
        Image icon = AddImage(iconRect, placeholder, Color.white, Image.Type.Simple);
        icon.preserveAspect = true;

        // Small level number in the bottom-right corner; the badge grows to fit "MAX".
        RectTransform badge = CreateRect("Level Badge", slot);
        Anchor(badge, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(4f, -4f), new Vector2(20f, 20f));
        AddImage(badge, sprites.Panel, PanelDark, Image.Type.Sliced);
        HorizontalLayoutGroup badgeLayout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
        badgeLayout.padding = new RectOffset(4, 4, 2, 2);
        badgeLayout.childAlignment = TextAnchor.MiddleCenter;
        badgeLayout.childControlWidth = true;
        badgeLayout.childControlHeight = true;
        badgeLayout.childForceExpandWidth = false;
        badgeLayout.childForceExpandHeight = false;
        ContentSizeFitter badgeFitter = badge.gameObject.AddComponent<ContentSizeFitter>();
        badgeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        badgeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        RectTransform badgeTextRect = CreateRect("Text", badge);
        Text badgeText = AddText(badgeTextRect, badgeFont, 16, TextAnchor.MiddleCenter, Color.white, "1", outline: false);
        badge.gameObject.SetActive(false);

        HUDSlotView view = slot.gameObject.AddComponent<HUDSlotView>();
        view.Configure(frame, icon, badge.gameObject, badgeText, placeholder);
        SerializedObject viewSo = new(view);
        viewSo.FindProperty("emptyIconAlpha").floatValue = 0.85f;
        viewSo.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ------------------------------------------------------------------ Sprite generation

    private static HUDSprites GenerateSprites()
    {
        Directory.CreateDirectory(SpriteFolder);
        Color clear = new(0f, 0f, 0f, 0f);
        Color innerDark = new(0.06f, 0.07f, 0.11f, 0.82f);

        HUDSprites result = new()
        {
            Pixel = WriteSprite("HUD_Pixel", 4, 4, (x, y) => Color.white, Vector4.zero),

            // 1 px ink outline, chamfered corners, white body (tinted by Image.color).
            Panel = WriteSprite(
                "HUD_Panel",
                8,
                8,
                (x, y) =>
                {
                    int d = EdgeDistance(x, y, 8, 8);
                    if (IsCorner(x, y, 8, 8))
                    {
                        return clear;
                    }

                    return d == 0 ? Ink : Color.white;
                },
                new Vector4(2, 2, 2, 2)
            ),

            // Vertical shading: bright top edge, darker bottom edge.
            BarFill = WriteSprite(
                "HUD_BarFill",
                4,
                8,
                (x, y) => y >= 6 ? Color.white : y == 0 ? Gray(0.62f) : Gray(0.84f),
                Vector4.zero
            ),

            // Ink outline, 1 px bevelled coloured rim (lit from top-left), dark glassy inside.
            // Border 2 px = 4 units at PixelScale, so a 32 unit icon fits a 40 unit slot exactly.
            SlotFrame = WriteSprite(
                "HUD_SlotFrame",
                12,
                12,
                (x, y) =>
                {
                    if (IsCorner(x, y, 12, 12))
                    {
                        return clear;
                    }

                    int d = EdgeDistance(x, y, 12, 12);
                    if (d == 0)
                    {
                        return Ink;
                    }

                    if (d == 1)
                    {
                        bool lit = x == 1 || y == 10;
                        bool shade = x == 10 || y == 1;
                        return lit && !shade ? Color.white : shade && !lit ? Gray(0.55f) : Gray(0.8f);
                    }

                    return innerDark;
                },
                new Vector4(2, 2, 2, 2)
            ),

            // 44 px = 1 ink + 2 ring track + 1 ink + 36 inner + 1 ink + 2 track + 1 ink.
            PortraitBack = WriteSprite(
                "HUD_PortraitBack",
                PortraitSpritePixels,
                PortraitSpritePixels,
                (x, y) =>
                {
                    if (IsCorner(x, y, PortraitSpritePixels, PortraitSpritePixels))
                    {
                        return clear;
                    }

                    int d = EdgeDistance(x, y, PortraitSpritePixels, PortraitSpritePixels);
                    return d switch
                    {
                        0 => Ink,
                        1 or 2 => new Color(0.17f, 0.19f, 0.28f, 1f),
                        3 => Ink,
                        _ => new Color(0.1f, 0.12f, 0.19f, 1f),
                    };
                },
                Vector4.zero
            ),

            // The 2 px band that sits on the track; radial-filled by the cooldown.
            Ring = WriteSprite(
                "HUD_UltimateRing",
                PortraitSpritePixels,
                PortraitSpritePixels,
                (x, y) =>
                {
                    int d = EdgeDistance(x, y, PortraitSpritePixels, PortraitSpritePixels);
                    if (d != 1 && d != 2)
                    {
                        return clear;
                    }

                    // Inner band is slightly darker so the ring reads as a rounded rim.
                    return d == 1 ? Color.white : Gray(0.82f);
                },
                Vector4.zero
            ),
        };

        return result;
    }

    private static int EdgeDistance(int x, int y, int w, int h)
    {
        return Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
    }

    private static bool IsCorner(int x, int y, int w, int h)
    {
        return (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
    }

    private static Color Gray(float value)
    {
        return new Color(value, value, value, 1f);
    }

    private static Sprite WriteSprite(string name, int width, int height, Func<int, int, Color> pixel, Vector4 border)
    {
        string path = $"{SpriteFolder}/{name}.png";
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, pixel(x, y));
            }
        }

        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void UsePointFilter(string folder)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }

    private static void UseRasterFont(string path)
    {
        // Smooth rendering anti-aliases the glyph edges, which is what makes pixel fonts look blurry.
        if (AssetImporter.GetAtPath(path) is TrueTypeFontImporter importer
            && importer.fontRenderingMode != FontRenderingMode.HintedRaster)
        {
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.SaveAndReimport();
        }
    }

    private static void UseSingleSprite(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        // Multiple-mode textures: take the first sub-sprite.
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite subSprite)
            {
                return subSprite;
            }
        }

        Debug.LogWarning($"[HUD] Sprite not found: {path}");
        return null;
    }

    // ------------------------------------------------------------------ UI helpers

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static Image AddImage(RectTransform rect, Sprite sprite, Color color, Image.Type type)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        image.raycastTarget = false;
        if (type == Image.Type.Sliced)
        {
            // 1 sprite pixel = PixelScale units, matching the portrait and skull.
            image.pixelsPerUnitMultiplier = 1f / PixelScale;
        }

        return image;
    }

    private static Text AddText(
        RectTransform rect,
        Font font,
        int size,
        TextAnchor alignment,
        Color color,
        string value,
        bool outline = true
    )
    {
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        if (outline)
        {
            Outline effect = rect.gameObject.AddComponent<Outline>();
            effect.effectColor = Ink;
            effect.effectDistance = new Vector2(2f, -2f);
        }

        return text;
    }

    private static void AssignArray<T>(SerializedProperty property, IReadOnlyList<T> values) where T : UnityEngine.Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
