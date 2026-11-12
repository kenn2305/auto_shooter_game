using UnityEngine;

/// <summary>
/// Sprites, fonts and colours of every menu (title, character select, options, pause, game over).
/// Sprites come from Assets/Theme/Theme Wood and are 9-sliced. Menus are laid out on a 640x360 virtual
/// screen scaled by whole numbers, so 1 sprite pixel = 1 virtual pixel.
/// </summary>
[CreateAssetMenu(fileName = "MenuTheme", menuName = "Game/Menu Theme")]
public sealed class MenuTheme : ScriptableObject
{
    [Header("Fonts (pixel fonts: body 8 px, headings 16 px multiples)")]
    public Font bodyFont;
    public Font headingFont;

    [Header("Panels")]
    public Sprite panel;
    public Sprite panelInterior;
    public Sprite panelDark;
    public Sprite panelDarker;
    public Sprite cell;
    public Sprite focus;

    [Header("Buttons")]
    public Sprite buttonNormal;
    public Sprite buttonHover;
    public Sprite buttonPressed;
    public Sprite buttonDisabled;

    [Header("Tabs")]
    public Sprite tabSelected;
    public Sprite tabUnselected;
    public Sprite tabHover;

    [Header("Controls")]
    public Sprite sliderTrack;
    public Sprite sliderFill;
    public Sprite sliderHandle;
    public Sprite toggleOn;
    public Sprite toggleOff;
    public Sprite arrowLeft;
    public Sprite arrowRight;
    public Sprite arrowRightHover;

    [Header("Colours")]
    public Color text = new(0.96f, 0.93f, 0.86f, 1f);
    public Color textDim = new(0.72f, 0.66f, 0.58f, 1f);
    public Color textDark = new(0.17f, 0.12f, 0.1f, 1f);
    public Color accent = new(1f, 0.62f, 0.26f, 1f);
    public Color highlight = new(1f, 0.86f, 0.42f, 1f);
    public Color danger = new(0.93f, 0.33f, 0.27f, 1f);
    public Color backdrop = new(0.08f, 0.06f, 0.05f, 0.86f);
}
