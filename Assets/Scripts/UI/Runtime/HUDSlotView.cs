using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One weapon or item square of the gameplay HUD: frame, icon and a small level badge.
/// An empty slot shows the faded placeholder icon and hides the badge.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDSlotView : MonoBehaviour
{
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject levelBadge;
    [SerializeField] private Text levelText;

    [SerializeField] private Sprite emptyIcon;
    [SerializeField, Range(0f, 1f)] private float emptyIconAlpha = 0.35f;

    private Object shownSource;
    private Sprite shownSprite;
    private int shownLevel = int.MinValue;
    private bool shownMax;

    public void Configure(Image frameImage, Image iconImage, GameObject badge, Text badgeText, Sprite placeholder)
    {
        frame = frameImage;
        icon = iconImage;
        levelBadge = badge;
        levelText = badgeText;
        emptyIcon = placeholder;
        SetEmpty();
    }

    public void SetEmpty()
    {
        if (shownSource == null && shownLevel == 0 && icon != null && icon.sprite == emptyIcon)
        {
            return;
        }

        shownSource = null;
        shownSprite = emptyIcon;
        shownLevel = 0;
        shownMax = false;
        if (icon != null)
        {
            icon.sprite = emptyIcon;
            icon.enabled = emptyIcon != null;
            icon.color = new Color(1f, 1f, 1f, emptyIconAlpha);
        }

        if (levelBadge != null)
        {
            levelBadge.SetActive(false);
        }
    }

    /// <param name="source">The equipped object; used only to skip redundant UI writes.</param>
    /// <param name="sprite">Its icon. Null keeps the placeholder at full opacity so the slot still reads as owned.</param>
    public void SetContent(Object source, Sprite sprite, int level, bool isMax)
    {
        if (source == shownSource && sprite == shownSprite && level == shownLevel && isMax == shownMax)
        {
            return;
        }

        shownSource = source;
        shownSprite = sprite;
        shownLevel = level;
        shownMax = isMax;

        if (icon != null)
        {
            icon.sprite = sprite != null ? sprite : emptyIcon;
            icon.enabled = icon.sprite != null;
            icon.color = Color.white;
        }

        if (levelBadge != null)
        {
            levelBadge.SetActive(level > 0);
        }

        if (levelText != null)
        {
            levelText.text = isMax ? "MAX" : level.ToString();
        }
    }
}
