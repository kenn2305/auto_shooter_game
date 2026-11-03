using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelUpDraftUI : MonoBehaviour
{
    private readonly List<Button> choiceButtons = new();
    private RectTransform cardContainer;
    private Text titleText;
    private Font runtimeFont;
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
        titleText.text = $"LEVEL {playerLevel} — CHOOSE ONE UPGRADE";
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

    private void BuildRuntimeView()
    {
        if (GetComponent<Canvas>() != null)
        {
            return;
        }

        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject backdrop = CreateUIObject("Backdrop", transform);
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.025f, 0.035f, 0.07f, 0.94f);
        Stretch(backdrop.GetComponent<RectTransform>());

        GameObject title = CreateUIObject("Title", backdrop.transform);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.08f, 0.81f);
        titleRect.anchorMax = new Vector2(0.92f, 0.94f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        titleText = CreateText(title, 42, TextAnchor.MiddleCenter, Color.white);
        titleText.fontStyle = FontStyle.Bold;

        GameObject cards = CreateUIObject("Choices", backdrop.transform);
        cardContainer = cards.GetComponent<RectTransform>();
        cardContainer.anchorMin = new Vector2(0.06f, 0.17f);
        cardContainer.anchorMax = new Vector2(0.94f, 0.78f);
        cardContainer.offsetMin = Vector2.zero;
        cardContainer.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = cards.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 32f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        GameObject footer = CreateUIObject("Footer", backdrop.transform);
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.1f, 0.06f);
        footerRect.anchorMax = new Vector2(0.9f, 0.13f);
        footerRect.offsetMin = Vector2.zero;
        footerRect.offsetMax = Vector2.zero;
        Text footerText = CreateText(footer, 22, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f));
        footerText.text = "Game paused — click a card or use keyboard/gamepad navigation";
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
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = GetCardColor(choice.Kind);
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.28f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = card.AddComponent<Button>();
        button.targetGraphic = cardImage;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.selectedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        int capturedIndex = index;
        button.onClick.AddListener(() => Select(capturedIndex));
        choiceButtons.Add(button);

        LayoutElement cardLayout = card.AddComponent<LayoutElement>();
        cardLayout.minWidth = 320f;
        cardLayout.preferredWidth = 430f;
        cardLayout.minHeight = 480f;

        VerticalLayoutGroup vertical = card.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(28, 28, 24, 24);
        vertical.spacing = 14f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = false;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        GameObject typeObject = CreateUIObject("Type", card.transform);
        Text typeText = CreateText(typeObject, 20, TextAnchor.MiddleCenter, new Color(0.82f, 0.88f, 1f));
        typeText.text = GetTypeLabel(choice.Kind);
        typeText.fontStyle = FontStyle.Bold;
        SetPreferredHeight(typeObject, 34f);

        GameObject iconObject = CreateUIObject("Icon", card.transform);
        Image icon = iconObject.AddComponent<Image>();
        icon.sprite = choice.Icon;
        icon.preserveAspect = true;
        icon.color = choice.Icon != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
        SetPreferredHeight(iconObject, 120f);

        GameObject titleObject = CreateUIObject("Name", card.transform);
        Text cardTitle = CreateText(titleObject, 30, TextAnchor.MiddleCenter, Color.white);
        cardTitle.text = choice.Title;
        cardTitle.fontStyle = FontStyle.Bold;
        SetPreferredHeight(titleObject, 84f);

        GameObject descriptionObject = CreateUIObject("Description", card.transform);
        Text description = CreateText(descriptionObject, 22, TextAnchor.UpperCenter, new Color(0.92f, 0.94f, 1f));
        description.text = string.IsNullOrWhiteSpace(choice.Description)
            ? "Upgrade this option."
            : choice.Description;
        description.horizontalOverflow = HorizontalWrapMode.Wrap;
        description.verticalOverflow = VerticalWrapMode.Truncate;
        SetPreferredHeight(descriptionObject, 160f);
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

    private Text CreateText(GameObject owner, int fontSize, TextAnchor alignment, Color color)
    {
        Text text = owner.AddComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.supportRichText = true;
        return text;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject result = new(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetPreferredHeight(GameObject owner, float height)
    {
        LayoutElement layout = owner.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
    }

    private static string GetTypeLabel(LevelUpChoiceKind kind)
    {
        return kind switch
        {
            LevelUpChoiceKind.NewWeapon => "NEW WEAPON",
            LevelUpChoiceKind.WeaponUpgrade => "WEAPON UPGRADE",
            LevelUpChoiceKind.NewItem => "NEW ITEM",
            LevelUpChoiceKind.ItemUpgrade => "ITEM UPGRADE",
            _ => "STAT PACKAGE",
        };
    }

    private static Color GetCardColor(LevelUpChoiceKind kind)
    {
        return kind switch
        {
            LevelUpChoiceKind.NewWeapon => new Color(0.08f, 0.27f, 0.58f, 0.98f),
            LevelUpChoiceKind.WeaponUpgrade => new Color(0.1f, 0.34f, 0.68f, 0.98f),
            LevelUpChoiceKind.NewItem => new Color(0.08f, 0.46f, 0.28f, 0.98f),
            LevelUpChoiceKind.ItemUpgrade => new Color(0.1f, 0.55f, 0.32f, 0.98f),
            _ => new Color(0.68f, 0.31f, 0.07f, 0.98f),
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
