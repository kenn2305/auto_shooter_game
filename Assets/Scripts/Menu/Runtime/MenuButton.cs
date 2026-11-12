using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Menu button: mouse hover selects it (so mouse and keyboard share one cursor), the selected button shows a
/// pointer arrow and a bright label, Submit/click runs the action.
/// </summary>
public sealed class MenuButton : Button, IPointerEnterHandler
{
    private Text label;
    private Action action;
    private Image pointer;
    private Color labelColor = Color.white;

    public Text Label => label;

    /// <summary>Cards and portraits draw their own selection frame instead of the side arrow.</summary>
    public bool ShowPointer { get; set; } = true;

    public event Action Selected;

    public void Setup(Text text, Action onClick)
    {
        label = text;
        labelColor = text != null ? text.color : Color.white;
        action = onClick;
        this.onClick.AddListener(Invoke);

        MenuTheme theme = MenuUI.Theme;
        if (theme != null && theme.arrowRight != null)
        {
            RectTransform rect = MenuUI.Rect("Pointer", transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-2f, 0f), new Vector2(16f, 16f));
            pointer = rect.gameObject.AddComponent<Image>();
            pointer.sprite = theme.arrowRightHover != null ? theme.arrowRightHover : theme.arrowRight;
            pointer.raycastTarget = false;
            pointer.enabled = false;
        }
    }

    public void SetAction(Action onClick) => action = onClick;

    public void SetText(string text)
    {
        if (label != null)
            label.text = text;
    }

    private void Invoke()
    {
        if (IsInteractable())
            action?.Invoke();
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        SetHighlighted(true);
        Selected?.Invoke();
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        SetHighlighted(false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        SetHighlighted(false);
    }

    private void SetHighlighted(bool on)
    {
        if (pointer != null)
            pointer.enabled = on && ShowPointer;
        if (label != null)
        {
            MenuTheme theme = MenuUI.Theme;
            label.color = !IsInteractable() && theme != null ? theme.textDim
                : on && theme != null ? theme.highlight : labelColor;
        }
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        if (label != null && state == SelectionState.Disabled && MenuUI.Theme != null)
            label.color = MenuUI.Theme.textDim;
        else if (label != null && state == SelectionState.Normal)
            label.color = labelColor;
    }
}
