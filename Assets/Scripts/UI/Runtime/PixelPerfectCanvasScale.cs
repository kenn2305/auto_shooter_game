using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps a pixel-art canvas crisp: the scale factor snaps to steps of 0.5 so pixel fonts land on whole
/// pixel sizes (sizes are multiples of 16 → 8 px multiples on screen), and every Font used under this
/// canvas samples its glyph atlas with Point filtering instead of bilinear.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasScaler))]
public sealed class PixelPerfectCanvasScale : MonoBehaviour
{
    [SerializeField, Min(1f)]
    [Tooltip(
        "Screen height at which the canvas is drawn at scale 1. Larger = smaller HUD. "
            + "1890 (= 1080 x 1.75) draws the HUD at half size on a 1080p screen."
    )]
    private float referenceHeight = 1890f;
    [SerializeField, Min(0.1f)] private float scaleStep = 0.5f;
    [SerializeField, Min(0.1f)] private float minimumScale = 0.5f;

    private readonly HashSet<Font> fonts = new();
    private CanvasScaler scaler;
    private int appliedHeight = -1;

    private void OnEnable()
    {
        scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        appliedHeight = -1;

        fonts.Clear();
        foreach (Text text in GetComponentsInChildren<Text>(true))
        {
            if (text.font != null)
            {
                fonts.Add(text.font);
            }
        }

        Font.textureRebuilt += HandleFontTextureRebuilt;
        foreach (Font font in fonts)
        {
            ApplyPointFilter(font);
        }

        ApplyScale();
    }

    /// <summary>Sets the scaling rule from code (canvases built at runtime) and applies it now.</summary>
    public void Configure(float height, float step, float minimum)
    {
        referenceHeight = Mathf.Max(1f, height);
        scaleStep = Mathf.Max(0.1f, step);
        minimumScale = Mathf.Max(0.1f, minimum);
        if (scaler != null)
        {
            ApplyScale();
        }
    }

    private void OnDisable()
    {
        Font.textureRebuilt -= HandleFontTextureRebuilt;
    }

    private void Update()
    {
        if (Screen.height != appliedHeight)
        {
            ApplyScale();
        }
    }

    private void ApplyScale()
    {
        appliedHeight = Screen.height;
        float raw = Screen.height / referenceHeight;
        float snapped = Mathf.Round(raw / scaleStep) * scaleStep;
        scaler.scaleFactor = Mathf.Max(minimumScale, snapped);
    }

    private void HandleFontTextureRebuilt(Font font)
    {
        // The dynamic atlas is recreated whenever new glyphs/sizes are requested; it comes back bilinear.
        if (fonts.Contains(font))
        {
            ApplyPointFilter(font);
        }
    }

    private static void ApplyPointFilter(Font font)
    {
        Texture texture = font != null && font.material != null ? font.material.mainTexture : null;
        if (texture != null)
        {
            texture.filterMode = FilterMode.Point;
        }
    }
}
