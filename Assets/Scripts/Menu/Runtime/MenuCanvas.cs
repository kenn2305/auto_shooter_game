using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scales a menu canvas by whole numbers so the 640x360 virtual screen fills as much of the window as
/// possible (x3 at 1080p, x4 at 1440p) and pixel art / pixel fonts stay sharp; also samples the theme
/// fonts with Point filtering.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasScaler))]
public sealed class MenuCanvas : MonoBehaviour
{
    private CanvasScaler scaler;
    private int appliedWidth = -1;
    private int appliedHeight = -1;

    private void OnEnable()
    {
        scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;
        appliedWidth = appliedHeight = -1;
        Font.textureRebuilt += HandleFontRebuilt;
        PointFilter(MenuUI.BodyFont);
        PointFilter(MenuUI.HeadingFont);
        Apply();
    }

    private void OnDisable()
    {
        Font.textureRebuilt -= HandleFontRebuilt;
    }

    private void Update()
    {
        if (Screen.width != appliedWidth || Screen.height != appliedHeight)
            Apply();
    }

    private void Apply()
    {
        appliedWidth = Screen.width;
        appliedHeight = Screen.height;
        float scale = Mathf.Floor(Mathf.Min(Screen.width / MenuUI.VirtualWidth, Screen.height / MenuUI.VirtualHeight));
        scaler.scaleFactor = Mathf.Max(1f, scale);
    }

    private static void HandleFontRebuilt(Font font)
    {
        if (font == MenuUI.BodyFont || font == MenuUI.HeadingFont)
            PointFilter(font);
    }

    private static void PointFilter(Font font)
    {
        if (font != null && font.material != null && font.material.mainTexture != null)
            font.material.mainTexture.filterMode = FilterMode.Point;
    }
}
