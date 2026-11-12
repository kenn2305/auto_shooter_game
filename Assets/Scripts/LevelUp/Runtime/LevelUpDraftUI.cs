using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Level-up draft screen: a title plate and one card per choice.
/// Card layout (top to bottom, evenly spaced): icon in an inset frame, a name plate coloured by the
/// choice kind, and the description in an inset box. Cards use the ChoseUpgradeLabel pixel panel as a
/// 9-sliced background. The scene object (built by Tools > UI > Build Gameplay HUD) carries the font and
/// sprites; without it the screen still builds with the built-in font and plain colours.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelUpDraftUI : MonoBehaviour
{
    // Layout in 1080p units; the canvas scale snaps to whole steps so the pixel font stays sharp.
    private const float CardWidth = 360f;
    private const float CardHeight = 520f;
    private const float Padding = 24f; // card edge → content, and the gap between every block
    private const float IconFrameSize = 144f;
    private const float IconSize = 128f; // 32 px icons at 4x
    private const float NamePlateWidth = 280f;
    private const float NamePlateHeight = 64f;
    private const float CardSpacing = 48f;
    private const float PanelPixelScale = 4f; // one panel sprite pixel = 4 units

    [Header("Look (assigned by the HUD builder)")]
    [SerializeField] private Font font;
    [Tooltip("ChoseUpgradeLabel: dark panel with a 1 px light border; used 9-sliced for cards and the title.")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("HUD_Panel: white plate with an ink outline; tinted per choice kind for the name.")]
    [SerializeField] private Sprite plateSprite;
    [SerializeField, Min(1)] private int panelBorderPixels = 3;

    private readonly List<Button> choiceButtons = new();
    private RectTransform cardContainer;
    private Text titleText;
    private Font runtimeFont;
    private Sprite slicedPanel;
    private Action<int> selectionCallback;

    public bool IsVisible => gameObject.activeSelf;

    public static LevelUpDraftUI CreateRuntime()
    {
        GameObject root = new("Level Up Draft UI", typeof(RectTransform));
        LevelUpDraftUI view = root.AddComponent<LevelUpDraftUI>();
        view.BuildRuntimeView();
        root.SetActive(false);
        return view;
    }

    public void Show(int playerLevel, IReadOnlyList<LevelUpChoice> choices, Action<int> onSelected)
    {
        if (cardContainer == null)
        {
            BuildRuntimeView();
        }

        selectionCallback = onSelected;
        titleText.text = $"LEVEL {playerLevel}  -  CHOOSE ONE UPGRADE";
        RebuildCards(choices);
        gameObject.SetActive(true);
        EnsureEventSystem();

        if (choiceButtons.Count > 0 && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(choiceButtons[0].gameObject);
        }
    }

    public void Hide()
    {
        selectionCallback = null;
        gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ Build

    private void BuildRuntimeView()
    {
        if (GetComponent<Canvas>() != null)
        {
            return;
        }

        runtimeFont = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        slicedPanel = CreateSlicedPanel();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvas.pixelPerfect = true;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = GetComponent<RectTransform>();
        Stretch(rootRect, 0f);

        GameObject backdrop = CreateUIObject("Backdrop", transform);
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.02f, 0.025f, 0.05f, 0.82f);
        Stretch(backdrop.GetComponent<RectTransform>(), 0f);

        // Title plate, centred above the cards.
        RectTransform titlePlate = CreateUIObject("Title", backdrop.transform).GetComponent<RectTransform>();
        Place(titlePlate, new Vector2(0.5f, 0.5f), new Vector2(0f, CardHeight * 0.5f + 88f), new Vector2(1000f, 80f));
        AddPanel(titlePlate.gameObject, Color.white);
        RectTransform titleTextRect = CreateUIObject("Text", titlePlate).GetComponent<RectTransform>();
        Stretch(titleTextRect, 0f);
        titleText = CreateText(titleTextRect.gameObject, 32, TextAnchor.MiddleCenter, Color.white, outline: true);

        // Cards row, centred on screen.
        cardContainer = CreateUIObject("Choices", backdrop.transform).GetComponent<RectTransform>();
        Place(cardContainer, new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(1600f, CardHeight));
        HorizontalLayoutGroup layout = cardContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = CardSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform footer = CreateUIObject("Footer", backdrop.transform).GetComponent<RectTransform>();
        Place(footer, new Vector2(0.5f, 0.5f), new Vector2(0f, -(CardHeight * 0.5f + 56f)), new Vector2(1200f, 32f));
        Text footerText = CreateText(footer.gameObject, 16, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f));
        footerText.text = "Game paused - click a card or use keyboard / gamepad";

        // Integer scale at 1080p and up: 1x up to ~1620p, 2x at 4K. Added last so it sees every Text.
        PixelPerfectCanvasScale pixelScale = gameObject.AddComponent<PixelPerfectCanvasScale>();
        pixelScale.Configure(1080f, 1f, 1f);
    }

    private void RebuildCards(IReadOnlyList<LevelUpChoice> choices)
    {
        for (int i = cardContainer.childCount - 1; i >= 0; i--)
        {
            GameObject oldCard = cardContainer.GetChild(i).gameObject;
            oldCard.SetActive(false);
            Destroy(oldCard);
        }

        choiceButtons.Clear();
        for (int i = 0; i < choices.Count; i++)
        {
            CreateChoiceCard(choices[i], i);
        }
    }

    private void CreateChoiceCard(LevelUpChoice choice, int index)
    {
        GameObject card = CreateUIObject($"Choice {index + 1}", cardContainer);
        Image cardImage = AddPanel(card, Color.white);
        cardImage.raycastTarget = true;

        LayoutElement cardLayout = card.AddComponent<LayoutElement>();
        cardLayout.minWidth = cardLayout.preferredWidth = CardWidth;
        cardLayout.minHeight = cardLayout.preferredHeight = CardHeight;

        Button button = card.AddComponent<Button>();
        button.targetGraphic = cardImage;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.84f, 0.38f, 1f); // gold border when hovered/selected
        colors.selectedColor = new Color(1f, 0.84f, 0.38f, 1f);
        colors.pressedColor = new Color(0.85f, 0.68f, 0.3f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        int capturedIndex = index;
        button.onClick.AddListener(() => Select(capturedIndex));
        choiceButtons.Add(button);

        Color accent = GetKindColor(choice.Kind);
        float y = -Padding; // running top edge inside the card

        // 1) Icon in an inset frame.
        RectTransform iconFrame = CreateBlock(card.transform, "Icon Frame", ref y, IconFrameSize, IconFrameSize);
        AddPanel(iconFrame.gameObject, new Color(0.7f, 0.72f, 0.8f, 1f));
        RectTransform iconRect = CreateUIObject("Icon", iconFrame).GetComponent<RectTransform>();
        Place(iconRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(IconSize, IconSize));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = choice.Icon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = choice.Icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        if (choice.Icon == null)
        {
            // Stat packages have no icon: a big "+" in the kind colour keeps the frame from looking empty.
            RectTransform glyphRect = CreateUIObject("Glyph", iconFrame).GetComponent<RectTransform>();
            Stretch(glyphRect, 0f);
            Text glyph = CreateText(glyphRect.gameObject, 64, TextAnchor.MiddleCenter, accent, outline: true);
            glyph.text = "+";
        }

        // 2) Name plate, coloured by kind.
        RectTransform namePlate = CreateBlock(card.transform, "Name", ref y, NamePlateWidth, NamePlateHeight);
        Image plate = namePlate.gameObject.AddComponent<Image>();
        plate.sprite = plateSprite;
        plate.type = plateSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        plate.pixelsPerUnitMultiplier = 1f / PanelPixelScale;
        plate.color = accent;
        plate.raycastTarget = false;
        RectTransform nameTextRect = CreateUIObject("Text", namePlate).GetComponent<RectTransform>();
        Stretch(nameTextRect, 8f);
        Text nameText = CreateText(nameTextRect.gameObject, 24, TextAnchor.MiddleCenter, Color.white, outline: true);
        nameText.text = choice.Title;
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        if (nameText.preferredWidth > NamePlateWidth - 16f)
        {
            nameText.fontSize = 16; // long names drop to the small size (still on the 8 px grid) instead of breaking words
            nameText.lineSpacing = 1.25f;
        }

        // 3) Description box fills the rest, keeping the same bottom padding.
        float remaining = CardHeight + y - Padding; // y is negative
        RectTransform descriptionBox = CreateBlock(card.transform, "Description", ref y, CardWidth - 2f * Padding, remaining);
        AddPanel(descriptionBox.gameObject, new Color(0.62f, 0.64f, 0.72f, 1f));
        RectTransform descriptionTextRect = CreateUIObject("Text", descriptionBox).GetComponent<RectTransform>();
        Stretch(descriptionTextRect, 16f);
        Text description = CreateText(descriptionTextRect.gameObject, 16, TextAnchor.UpperLeft, new Color(0.93f, 0.95f, 1f));
        description.text = string.IsNullOrWhiteSpace(choice.Description) ? "Upgrade this option." : choice.Description;
        description.horizontalOverflow = HorizontalWrapMode.Wrap;
        description.verticalOverflow = VerticalWrapMode.Truncate;
        description.lineSpacing = 1.15f;
    }

    /// <summary>Adds a horizontally centred block below <paramref name="top"/> and moves it past the block plus one gap.</summary>
    private RectTransform CreateBlock(Transform parent, string objectName, ref float top, float width, float height)
    {
        RectTransform rect = CreateUIObject(objectName, parent).GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, top);
        top -= height + Padding;
        return rect;
    }

    private Image AddPanel(GameObject owner, Color tint)
    {
        Image image = owner.AddComponent<Image>();
        image.sprite = slicedPanel;
        image.type = slicedPanel != null ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f / PanelPixelScale;
        image.color = slicedPanel != null ? tint : tint * new Color(0.2f, 0.2f, 0.22f, 1f);
        image.raycastTarget = false;
        return image;
    }

    /// <summary>The imported label has no 9-slice border; make a bordered copy that shares its texture.</summary>
    private Sprite CreateSlicedPanel()
    {
        if (panelSprite == null)
        {
            return null;
        }

        float border = panelBorderPixels;
        Sprite sliced = Sprite.Create(
            panelSprite.texture,
            panelSprite.rect,
            new Vector2(0.5f, 0.5f),
            panelSprite.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border)
        );
        sliced.name = $"{panelSprite.name} (9-slice)";
        return sliced;
    }

    private void Select(int index)
    {
        Action<int> callback = selectionCallback;
        selectionCallback = null;
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            choiceButtons[i].interactable = false;
        }

        callback?.Invoke(index);
    }

    private void OnDestroy()
    {
        if (slicedPanel != null)
        {
            Destroy(slicedPanel);
        }
    }

    // ------------------------------------------------------------------ Helpers

    private Text CreateText(GameObject owner, int fontSize, TextAnchor alignment, Color color, bool outline = false)
    {
        Text text = owner.AddComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        if (outline)
        {
            Outline effect = owner.AddComponent<Outline>();
            effect.effectColor = new Color(0.04f, 0.04f, 0.07f, 1f);
            effect.effectDistance = new Vector2(2f, -2f);
        }

        return text;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject result = new(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static Color GetKindColor(LevelUpChoiceKind kind)
    {
        return kind switch
        {
            LevelUpChoiceKind.NewWeapon => new Color(0.86f, 0.36f, 0.42f, 1f),
            LevelUpChoiceKind.WeaponUpgrade => new Color(0.86f, 0.36f, 0.42f, 1f),
            LevelUpChoiceKind.NewItem => new Color(0.3f, 0.66f, 0.38f, 1f),
            LevelUpChoiceKind.ItemUpgrade => new Color(0.3f, 0.66f, 0.38f, 1f),
            LevelUpChoiceKind.WeaponEvolution => new Color(0.6f, 0.36f, 0.82f, 1f),
            _ => new Color(0.82f, 0.56f, 0.24f, 1f),
        };
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }
}
