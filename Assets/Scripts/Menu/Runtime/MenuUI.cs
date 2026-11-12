using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds menu widgets from the <see cref="MenuTheme"/>. Every menu is laid out on a 640x360 virtual
/// screen (see <see cref="MenuCanvas"/>), so sizes below are in sprite pixels.
/// </summary>
public static class MenuUI
{
    public const float VirtualWidth = 640f;
    public const float VirtualHeight = 360f;

    public static MenuTheme Theme => GameCatalog.Instance != null ? GameCatalog.Instance.theme : null;

    private static Font fallbackFont;

    public static Font BodyFont => Theme != null && Theme.bodyFont != null ? Theme.bodyFont : Fallback;
    public static Font HeadingFont => Theme != null && Theme.headingFont != null ? Theme.headingFont : BodyFont;

    private static Font Fallback =>
        fallbackFont != null ? fallbackFont : fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    // ───────────────────────────── canvas ─────────────────────────────

    public static RectTransform CreateCanvas(string name, int sortingOrder, Transform parent = null)
    {
        EnsureEventSystem();
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvas.pixelPerfect = true;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        go.AddComponent<MenuCanvas>();

        // Content lives in a fixed 640x360 frame centred on the screen.
        RectTransform frame = Rect("Frame", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(VirtualWidth, VirtualHeight));
        return frame;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    // ───────────────────────────── layout ─────────────────────────────

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position,
        Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>Rect from the top-left corner of the parent (x right, y down), like a pixel mock-up.</summary>
    public static RectTransform Box(string name, Transform parent, float x, float y, float width, float height)
    {
        return Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y),
            new Vector2(width, height));
    }

    public static RectTransform Stretch(string name, Transform parent, float inset = 0f)
    {
        var rect = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    // ───────────────────────────── visuals ─────────────────────────────

    public static Image Panel(RectTransform rect, Sprite sprite, Color? tint = null)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = tint ?? (sprite != null ? Color.white : new Color(0.2f, 0.15f, 0.12f, 0.95f));
        return image;
    }

    /// <summary>The theme's framed window: outer wooden frame + inner panel.</summary>
    public static RectTransform Window(string name, Transform parent, float x, float y, float width, float height,
        out RectTransform content)
    {
        MenuTheme t = Theme;
        RectTransform frame = Box(name, parent, x, y, width, height);
        Panel(frame, t != null ? t.panel : null);
        RectTransform inner = Stretch("Inner", frame, 6f);
        Panel(inner, t != null ? t.panelInterior : null);
        content = Stretch("Content", inner, 6f);
        return frame;
    }

    public static Image Icon(Transform parent, Sprite sprite, float x, float y, float size)
    {
        RectTransform rect = Box("Icon", parent, x, y, size, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    public static Text Label(Transform parent, string text, float x, float y, float width, float height,
        int size = 8, TextAnchor align = TextAnchor.UpperLeft, Color? color = null, bool heading = false,
        bool shadow = false)
    {
        RectTransform rect = Box("Text", parent, x, y, width, height);
        return LabelOn(rect, text, size, align, color, heading, shadow);
    }

    public static Text LabelOn(RectTransform rect, string text, int size = 8, TextAnchor align = TextAnchor.UpperLeft,
        Color? color = null, bool heading = false, bool shadow = false)
    {
        var label = rect.gameObject.AddComponent<Text>();
        label.font = heading ? HeadingFont : BodyFont;
        label.fontSize = size;
        label.alignment = align;
        label.color = color ?? (Theme != null ? Theme.text : Color.white);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.supportRichText = true;
        label.raycastTarget = false;
        label.lineSpacing = 1.1f;
        label.text = text;
        if (shadow)
        {
            var effect = rect.gameObject.AddComponent<Shadow>();
            effect.effectColor = new Color(0.08f, 0.05f, 0.04f, 1f);
            effect.effectDistance = new Vector2(1f, -1f);
        }

        return label;
    }

    // ───────────────────────────── controls ─────────────────────────────

    public static MenuButton MakeButton(Transform parent, string text, float x, float y, float width, float height,
        Action onClick, int fontSize = 16)
    {
        MenuTheme t = Theme;
        RectTransform rect = Box($"Button {text}", parent, x, y, width, height);
        Image image = Panel(rect, t != null ? t.buttonNormal : null);

        var button = rect.gameObject.AddComponent<MenuButton>();
        button.targetGraphic = image;
        if (t != null && t.buttonNormal != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = t.buttonHover,
                selectedSprite = t.buttonHover,
                pressedSprite = t.buttonPressed,
                disabledSprite = t.buttonDisabled,
            };
        }

        RectTransform labelRect = Stretch("Label", rect);
        labelRect.offsetMin = new Vector2(4f, 0f);
        labelRect.offsetMax = new Vector2(-4f, -1f);
        Text label = LabelOn(labelRect, text, fontSize, TextAnchor.MiddleCenter, t != null ? t.text : Color.white,
            heading: true, shadow: true);
        button.Setup(label, onClick);
        return button;
    }

    public static Slider MakeSlider(Transform parent, float x, float y, float width, float value, Action<float> onChange)
    {
        MenuTheme t = Theme;
        RectTransform rect = Box("Slider", parent, x, y, width, 12f);
        RectTransform track = Stretch("Track", rect);
        track.offsetMin = new Vector2(0f, 2f);
        track.offsetMax = new Vector2(0f, -2f);
        Panel(track, t != null ? t.sliderTrack : null);

        RectTransform fillArea = Stretch("Fill Area", rect);
        fillArea.offsetMin = new Vector2(1f, 2f);
        fillArea.offsetMax = new Vector2(-6f, -2f);
        RectTransform fill = Stretch("Fill", fillArea);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.sizeDelta = new Vector2(6f, 0f);
        Panel(fill, t != null ? t.sliderFill : null);

        RectTransform handleArea = Stretch("Handle Area", rect);
        handleArea.offsetMin = new Vector2(6f, 0f);
        handleArea.offsetMax = new Vector2(-6f, 0f);
        RectTransform handle = Rect("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(13f, 11f));
        Image handleImage = Panel(handle, t != null ? t.sliderHandle : null);

        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        ColorBlock colors = slider.colors;
        colors.selectedColor = colors.highlightedColor = t != null ? t.highlight : Color.yellow;
        slider.colors = colors;
        slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
        return slider;
    }

    /// <summary>A row "label ........ [x]" that flips a bool when clicked.</summary>
    public static MenuButton MakeToggleRow(Transform parent, string text, float x, float y, float width, bool value,
        Action<bool> onChange)
    {
        MenuTheme t = Theme;
        MenuButton row = MakeButton(parent, text, x, y, width, 18f, null, 8);
        row.Label.alignment = TextAnchor.MiddleLeft;
        row.Label.rectTransform.offsetMin = new Vector2(8f, 0f);

        Image check = Icon(row.transform, null, width - 22f, 1f, 16f);
        bool state = value;
        void Refresh()
        {
            check.sprite = state ? t?.toggleOn : t?.toggleOff;
            check.enabled = check.sprite != null;
        }

        Refresh();
        row.SetAction(() =>
        {
            state = !state;
            Refresh();
            onChange?.Invoke(state);
        });
        return row;
    }

    /// <summary>Selects <paramref name="target"/> for keyboard navigation (and shows its cursor).</summary>
    public static void Select(Selectable target)
    {
        if (target == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    /// <summary>Explicit up/down navigation through a vertical list (wraps around).</summary>
    public static void LinkVertical(IList<Selectable> items, bool wrap = true)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Navigation nav = items[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? items[i - 1] : wrap ? items[items.Count - 1] : null;
            nav.selectOnDown = i < items.Count - 1 ? items[i + 1] : wrap ? items[0] : null;
            items[i].navigation = nav;
        }
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
