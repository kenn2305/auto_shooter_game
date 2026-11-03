using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
public sealed class ExperiencePickup : MonoBehaviour
{
    private const int SpriteSize = 32;
    private static Sprite triangleSprite;

    private SpriteRenderer spriteRenderer;
    private CircleCollider2D pickupCollider;
    private ExperiencePickupType type;
    private int value;
    private float attractionSpeed;
    private bool attracting;

    public ExperiencePickupType Type => type;
    public int Value => value;
    public bool IsAttracting => attracting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticSprite()
    {
        triangleSprite = null;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        pickupCollider = GetComponent<CircleCollider2D>();
        pickupCollider.isTrigger = true;
        pickupCollider.radius = 0.45f;
        spriteRenderer.sprite = GetOrCreateTriangleSprite();
        spriteRenderer.sortingOrder = 20;
    }

    public void Activate(
        ExperiencePickupType pickupType,
        int experienceValue,
        Vector3 position,
        ExperienceSystemConfig config
    )
    {
        type = pickupType;
        value = experienceValue;
        attracting = false;
        attractionSpeed = Mathf.Max(0f, config.initialAttractionSpeed);

        transform.SetPositionAndRotation(position, Quaternion.identity);
        float size = type == ExperiencePickupType.RainbowMagnet ? config.rainbowWorldSize : config.regularWorldSize;
        transform.localScale = Vector3.one * size;
        spriteRenderer.sprite = config.pickupSprite != null ? config.pickupSprite : GetOrCreateTriangleSprite();
        spriteRenderer.color = config.GetColor(type);
        gameObject.SetActive(true);
    }

    public void BeginAttraction()
    {
        attracting = true;
    }

    public bool Tick(Transform target, float pickupRadiusSquared, ExperienceSystemConfig config)
    {
        if (target == null)
        {
            return false;
        }

        Vector2 offset = target.position - transform.position;
        float distanceSquared = offset.sqrMagnitude;
        if (!attracting && distanceSquared <= pickupRadiusSquared)
        {
            attracting = true;
        }

        if (attracting)
        {
            attractionSpeed = Mathf.Min(
                config.maximumAttractionSpeed,
                attractionSpeed + config.attractionAcceleration * Time.deltaTime
            );
            transform.position = Vector3.MoveTowards(
                transform.position,
                target.position,
                attractionSpeed * Time.deltaTime
            );

            distanceSquared = ((Vector2)target.position - (Vector2)transform.position).sqrMagnitude;
            if (distanceSquared <= config.collectionDistance * config.collectionDistance)
            {
                return true;
            }
        }

        if (type == ExperiencePickupType.RainbowMagnet)
        {
            float hue = Mathf.Repeat(Time.unscaledTime * config.rainbowHueSpeed, 1f);
            spriteRenderer.color = Color.HSVToRGB(hue, 0.75f, 1f);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * config.rainbowPulseSpeed) * config.rainbowPulseAmount;
            transform.localScale = Vector3.one * config.rainbowWorldSize * pulse;
        }

        return false;
    }

    public void Deactivate()
    {
        attracting = false;
        gameObject.SetActive(false);
    }

    private static Sprite GetOrCreateTriangleSprite()
    {
        if (triangleSprite != null)
        {
            return triangleSprite;
        }

        Texture2D texture = new(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
        {
            name = "Runtime EXP Triangle",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        Color clear = new(1f, 1f, 1f, 0f);
        Color white = Color.white;
        for (int y = 0; y < SpriteSize; y++)
        {
            float normalizedY = y / (float)(SpriteSize - 1);
            float halfWidth = (1f - normalizedY) * 0.46f;
            for (int x = 0; x < SpriteSize; x++)
            {
                float normalizedX = x / (float)(SpriteSize - 1);
                bool inside = normalizedY >= 0.06f && Mathf.Abs(normalizedX - 0.5f) <= halfWidth;
                texture.SetPixel(x, y, inside ? white : clear);
            }
        }

        texture.Apply(false, true);
        triangleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, SpriteSize, SpriteSize),
            new Vector2(0.5f, 0.5f),
            SpriteSize
        );
        triangleSprite.name = "Runtime EXP Triangle Sprite";
        triangleSprite.hideFlags = HideFlags.HideAndDontSave;
        return triangleSprite;
    }
}
