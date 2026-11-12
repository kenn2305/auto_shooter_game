using UnityEngine;
using UnityEngine.UI;

/// <summary>Loops sprite frames on a UI Image with unscaled time (works while the game is paused).</summary>
[RequireComponent(typeof(Image))]
public sealed class MenuSpriteAnimator : MonoBehaviour
{
    private Image image;
    private Sprite[] frames;
    private float fps = 8f;
    private int shown = -1;

    public void Play(Sprite[] sequence, float framesPerSecond)
    {
        image = GetComponent<Image>();
        frames = sequence;
        fps = Mathf.Max(0.1f, framesPerSecond);
        shown = -1;
        if (frames != null && frames.Length > 0)
            image.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || frames.Length <= 1)
            return;

        int frame = (int)(Time.unscaledTime * fps) % frames.Length;
        if (frame != shown)
        {
            shown = frame;
            image.sprite = frames[frame];
        }
    }
}
