using UnityEngine;

/// <summary>
/// Green pixel health bar that always floats above the character's head.
/// Head height and body centre are measured per character by Tools > UI > Setup Player Health Bars,
/// so the bar sits just above the tallest animation frame and follows the body when the sprite flips.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerHealthBar : MonoBehaviour
{
    [Header("Placement (character root space)")]
    [Tooltip("Y of the top of the tallest animation frame, measured by the setup tool.")]
    [SerializeField] private float headTop = 1.2f;
    [Tooltip("Gap between the head and the bottom of the bar.")]
    [SerializeField, Min(0f)] private float gapAboveHead = 0.15f;
    [Tooltip("Horizontal body centre relative to the sprite pivot, facing right. Mirrored when the sprite flips.")]
    [SerializeField] private float bodyCenterX;

    [Header("Look (world units)")]
    [Tooltip("World size of one bar pixel; ~ one character pixel.")]
    [SerializeField, Min(0.01f)] private float pixelSize = 0.1f;
    [SerializeField, Min(2)] private int widthPixels = 16;
    [SerializeField, Min(1)] private int heightPixels = 2;
    [SerializeField] private Color outlineColor = new(0.05f, 0.05f, 0.07f, 1f);
    [SerializeField] private Color backColor = new(0.16f, 0.13f, 0.15f, 1f);
    [SerializeField] private Color trailColor = new(1f, 0.95f, 0.85f, 1f);
    [SerializeField] private Color fillColor = new(0.3f, 0.82f, 0.32f, 1f);
    [SerializeField] private Color highlightColor = new(0.62f, 1f, 0.55f, 1f);
    [SerializeField] private int sortingOrder = 500;

    [Header("Damage Trail")]
    [SerializeField, Min(0f)] private float trailDelay = 0.3f;
    [SerializeField, Min(0.1f)] private float trailSpeed = 1.5f;

    private static Sprite pixelSprite;

    private PlayerHealth health;
    private Transform barRoot;
    private Transform trail;
    private Transform fill;
    private Transform highlight;
    private SpriteRenderer[] ownRenderers;
    private SpriteRenderer body;
    private int shownFillPixels = -1;
    private int lastHealth = int.MinValue;
    private float trailValue = 1f;
    private float trailHold;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pixelSprite = null;
    }

    /// <summary>World point just above the health bar (heal numbers start here).</summary>
    public Vector3 AboveBarPosition =>
        barRoot != null
            ? barRoot.position + Vector3.up * ((heightPixels + 2) * pixelSize * 0.5f + pixelSize)
            : transform.position + Vector3.up * (headTop + gapAboveHead + 0.5f);

    public void Configure(float measuredHeadTop, float measuredBodyCenterX)
    {
        headTop = measuredHeadTop;
        bodyCenterX = measuredBodyCenterX;
    }

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        Build();
    }

    private void LateUpdate()
    {
        if (health == null)
        {
            return;
        }

        UpdatePlacement();

        int maximum = Mathf.Max(1, health.MaxHealth);
        int current = Mathf.Clamp(health.CurrentHealth, 0, maximum);
        float ratio = current / (float)maximum;
        if (lastHealth != int.MinValue && current < lastHealth)
        {
            trailHold = trailDelay;
        }

        lastHealth = current;

        // Whole bar pixels only; any health left keeps at least one pixel lit.
        int fillPixels = current <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(ratio * widthPixels), 1, widthPixels);
        if (fillPixels != shownFillPixels)
        {
            shownFillPixels = fillPixels;
            SetWidth(fill, fillPixels);
            SetWidth(highlight, fillPixels);
        }

        if (ratio >= trailValue)
        {
            trailValue = ratio;
        }
        else if (trailHold > 0f)
        {
            trailHold -= Time.deltaTime;
        }
        else
        {
            trailValue = Mathf.MoveTowards(trailValue, ratio, trailSpeed * Time.deltaTime);
        }

        int trailPixels = Mathf.Max(fillPixels, Mathf.CeilToInt(trailValue * widthPixels - 0.001f));
        SetWidth(trail, Mathf.Min(trailPixels, widthPixels));
    }

    private void UpdatePlacement()
    {
        if (body == null || !body.enabled || !body.gameObject.activeInHierarchy)
        {
            body = FindBodyRenderer();
        }

        float x = 0f;
        if (body != null)
        {
            float pivotX = transform.InverseTransformPoint(body.transform.position).x;
            x = pivotX + (body.flipX ? -bodyCenterX : bodyCenterX);
        }

        // Snap to the bar's pixel grid so it does not shimmer.
        x = Mathf.Round(x / pixelSize) * pixelSize;
        float y = headTop + gapAboveHead + (heightPixels + 2) * pixelSize * 0.5f;
        barRoot.localPosition = new Vector3(x, y, 0f);
    }

    private SpriteRenderer FindBodyRenderer()
    {
        // The animated model (Animator on the same object) is the body; weapons/effects are other sprites.
        SpriteRenderer fallback = null;
        foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (candidate.sprite == null || !candidate.enabled || System.Array.IndexOf(ownRenderers, candidate) >= 0)
            {
                continue;
            }

            if (candidate.GetComponent<Animator>() != null)
            {
                return candidate;
            }

            fallback ??= candidate;
        }

        return fallback;
    }

    private void Build()
    {
        barRoot = new GameObject("Health Bar").transform;
        barRoot.SetParent(transform, false);

        float width = widthPixels * pixelSize;
        float height = heightPixels * pixelSize;
        float left = -width * 0.5f;

        SpriteRenderer outline = CreateRect("Outline", outlineColor, 0, left - pixelSize, 0f, width + 2f * pixelSize, height + 2f * pixelSize);
        SpriteRenderer back = CreateRect("Back", backColor, 1, left, 0f, width, height);
        SpriteRenderer trailRenderer = CreateRect("Trail", trailColor, 2, left, 0f, width, height);
        SpriteRenderer fillRenderer = CreateRect("Fill", fillColor, 3, left, 0f, width, height);
        // Top pixel row of the fill is lighter, lit from above like the rest of the pixel art.
        SpriteRenderer highlightRenderer = CreateRect(
            "Highlight", highlightColor, 4, left, (height - pixelSize) * 0.5f, width, pixelSize
        );

        trail = trailRenderer.transform;
        fill = fillRenderer.transform;
        highlight = highlightRenderer.transform;
        ownRenderers = new[] { outline, back, trailRenderer, fillRenderer, highlightRenderer };
        UpdatePlacement();
    }

    private SpriteRenderer CreateRect(string objectName, Color color, int order, float left, float centerY, float width, float height)
    {
        GameObject go = new(objectName);
        go.transform.SetParent(barRoot, false);
        go.transform.localPosition = new Vector3(left, centerY, 0f);
        go.transform.localScale = new Vector3(width, height, 1f);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPixelSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder + order;
        return renderer;
    }

    private void SetWidth(Transform part, int pixels)
    {
        Vector3 scale = part.localScale;
        scale.x = pixels * pixelSize;
        part.localScale = scale;
        part.gameObject.SetActive(pixels > 0);
    }

    // 1x1 world-unit white square with its pivot on the left edge, so scaling X grows the bar rightwards.
    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
        {
            return pixelSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Health Bar Pixel",
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
        pixelSprite.name = "Health Bar Pixel";
        pixelSprite.hideFlags = HideFlags.HideAndDontSave;
        return pixelSprite;
    }
}
