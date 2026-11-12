using UnityEngine;

/// <summary>
/// Fades a tall map object (tree, pine, ruin, bush, stump) while the player or an enemy stands behind it,
/// so characters never get lost behind decoration.
///
/// "Behind" follows the Y sort used by the map: the object draws in front of a character whose centre is
/// above the object's feet (pivot). Only objects that sort by their pivot are occluders; MapStreamer adds
/// this component to them when it first instantiates them.
/// </summary>
[DisallowMultipleComponent]
public sealed class DecorOcclusionFader : MonoBehaviour
{
    private const float CheckInterval = 0.08f;

    [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.45f;
    [SerializeField, Min(0.1f)] private float fadeSpeed = 5f;
    [Tooltip("Shrinks the sprite box horizontally; the sprite rect has transparent margins.")]
    [SerializeField, Range(0.3f, 1f)] private float widthFactor = 0.8f;

    private static int characterMask = -1;

    private SpriteRenderer target;
    private float alpha = 1f;
    private float targetAlpha = 1f;
    private float nextCheck;

    /// <summary>Adds the fader to <paramref name="instance"/> when its main sprite is a Y-sorted occluder.</summary>
    public static void AttachIfOccluder(GameObject instance)
    {
        if (instance.GetComponent<DecorOcclusionFader>() != null)
        {
            return;
        }

        SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.spriteSortPoint == SpriteSortPoint.Pivot)
        {
            instance.AddComponent<DecorOcclusionFader>();
        }
    }

    private void Awake()
    {
        target = GetComponent<SpriteRenderer>();
        if (characterMask == -1)
        {
            characterMask = LayerMask.GetMask("Player", "Enemy");
        }
    }

    private void OnEnable()
    {
        // Pooled: every reuse starts fully opaque, and checks are spread over frames.
        alpha = 1f;
        targetAlpha = 1f;
        nextCheck = Time.time + Random.value * CheckInterval;
        Apply();
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + CheckInterval;
            targetAlpha = HasCharacterBehind() ? fadedAlpha : 1f;
        }

        if (!Mathf.Approximately(alpha, targetAlpha))
        {
            alpha = Mathf.MoveTowards(alpha, targetAlpha, fadeSpeed * Time.deltaTime);
            Apply();
        }
    }

    private bool HasCharacterBehind()
    {
        Bounds bounds = target.bounds;
        float feetY = transform.position.y;
        float top = bounds.max.y;
        if (top <= feetY)
        {
            return false;
        }

        // Only the part of the sprite above its feet can hide someone standing behind it.
        Vector2 size = new(bounds.size.x * widthFactor, top - feetY);
        Vector2 center = new(bounds.center.x, feetY + size.y * 0.5f);

        using PhysicsQuery.Results<Collider2D> hits = PhysicsQuery.OverlapBox(center, size, 0f, characterMask);
        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D hit = hits[i];
            if (hit.isTrigger)
            {
                continue; // hurt/hit boxes; the body collider decides
            }

            if (hit.bounds.center.y > feetY)
            {
                return true;
            }
        }

        return false;
    }

    private void Apply()
    {
        if (target == null)
        {
            return;
        }

        Color color = target.color;
        color.a = alpha;
        target.color = color;
    }
}
