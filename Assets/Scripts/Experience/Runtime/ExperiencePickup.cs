using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
public sealed class ExperiencePickup : MonoBehaviour
{
    private const int SpriteSize = 32;
    private const int GroundSortingOrder = -55;
    private static Sprite triangleSprite;

    private SpriteRenderer spriteRenderer;
    private CircleCollider2D pickupCollider;
    private ExperiencePickupType type;
    private int value;
    private float attractionSpeed;
    private bool attracting;
    // Cached world position: idle pickups never touch their Transform after spawning.
    private Vector2 position;

    // Animated sprite (optional): frames come from the config, each pickup starts at a random phase.
    private Sprite[] frames;
    private float framePhase;
    private int shownFrame = -1;
    private float baseScale = 1f;

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
        // Collection is distance-based (ExperiencePickupService); a live trigger would only make every
        // enemy walking over a gem generate physics contacts.
        pickupCollider.enabled = false;
        spriteRenderer.sprite = GetOrCreateTriangleSprite();
        // Lies on the ground: above ground tiles (-100), grass decor (-90) and decals (-60), under object
        // shadows (-50) and under every character and map object (0), so gems never cover them.
        spriteRenderer.sortingOrder = GroundSortingOrder;
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

        this.position = position;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        frames = config.GetFrames(type);
        shownFrame = -1;
        framePhase = Random.value * Mathf.Max(1, frames.Length);
        Sprite sprite = frames.Length > 0 && frames[0] != null
            ? frames[0]
            : config.pickupSprite != null ? config.pickupSprite : GetOrCreateTriangleSprite();
        spriteRenderer.sprite = sprite;
        // Authored frames carry their own colours; the tint only colours the plain fallback triangle.
        spriteRenderer.color = frames.Length > 0 ? Color.white : config.GetColor(type);

        // World size = height of the whole sprite, independent of its pixel size / PPU.
        float size = type == ExperiencePickupType.RainbowMagnet ? config.rainbowWorldSize : config.regularWorldSize;
        float spriteHeight = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.y) : 1f;
        baseScale = size / spriteHeight;
        transform.localScale = Vector3.one * baseScale;
        UpdateFrame(config);
        spriteRenderer.enabled = true;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void BeginAttraction()
    {
        attracting = true;
    }

    /// <summary>
    /// Advances the pickup toward <paramref name="targetPosition"/> once attracted. Returns true when it
    /// reached the collection distance. Called by ExperiencePickupService for every live pickup per frame.
    /// </summary>
    public bool Tick(Vector2 targetPosition, float pickupRadiusSquared, ExperienceSystemConfig config, float deltaTime)
    {
        float distanceSquared = (targetPosition - position).sqrMagnitude;
        if (!attracting && distanceSquared <= pickupRadiusSquared)
        {
            attracting = true;
        }

        if (attracting)
        {
            attractionSpeed = Mathf.Min(
                config.maximumAttractionSpeed,
                attractionSpeed + config.attractionAcceleration * deltaTime
            );
            position = Vector2.MoveTowards(position, targetPosition, attractionSpeed * deltaTime);
            transform.position = position;

            distanceSquared = (targetPosition - position).sqrMagnitude;
            if (distanceSquared <= config.collectionDistance * config.collectionDistance)
            {
                return true;
            }
        }

        UpdateFrame(config);

        if (type == ExperiencePickupType.RainbowMagnet)
        {
            if (frames.Length == 0)
            {
                float hue = Mathf.Repeat(Time.time * config.rainbowHueSpeed, 1f);
                spriteRenderer.color = Color.HSVToRGB(hue, 0.75f, 1f);
            }

            // Scaled time: freezes with the rest of the world while gameplay is paused.
            float pulse = 1f + Mathf.Sin(Time.time * config.rainbowPulseSpeed) * config.rainbowPulseAmount;
            transform.localScale = Vector3.one * baseScale * pulse;
        }

        return false;
    }

    private void UpdateFrame(ExperienceSystemConfig config)
    {
        if (frames == null || frames.Length <= 1)
        {
            return;
        }

        int frame = (int)(Time.time * config.pickupFramesPerSecond + framePhase) % frames.Length;
        if (frame != shownFrame && frames[frame] != null)
        {
            shownFrame = frame;
            spriteRenderer.sprite = frames[frame];
        }
    }

    public void Deactivate()
    {
        attracting = false;
        // Pooled pickups stay active and only hide their sprite: they have no Update and no live
        // collider, and toggling the GameObject costs far more when a magnet collects hundreds at once.
        spriteRenderer.enabled = false;
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
