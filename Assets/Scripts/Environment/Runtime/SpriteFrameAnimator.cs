using UnityEngine;

/// <summary>
/// Loops a sprite sequence on a map prop (altar runes, shrine water, chalice spirits). Frames are picked
/// from Time.time, so a pooled instance needs no state reset and copies of the same prop stay in sync
/// unless <see cref="randomPhase"/> is set. Only assigns the sprite when the frame changes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
    [SerializeField, Min(0.1f)] private float framesPerSecond = 8f;
    [Tooltip("Start each instance at a random frame so neighbouring copies do not animate in lockstep.")]
    [SerializeField] private bool randomPhase = true;

    private SpriteRenderer spriteRenderer;
    private float phase;
    private int shown = -1;

    public void Setup(Sprite[] sequence, float fps)
    {
        frames = sequence;
        framesPerSecond = Mathf.Max(0.1f, fps);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        phase = randomPhase && frames.Length > 0 ? Random.value * frames.Length : 0f;
        shown = -1;
    }

    private void Update()
    {
        if (frames.Length <= 1)
            return;

        int frame = (int)(Time.time * framesPerSecond + phase) % frames.Length;
        if (frame != shown)
        {
            shown = frame;
            spriteRenderer.sprite = frames[frame];
        }
    }
}
