using UnityEngine;

/// <summary>
/// Minimal example showing how gameplay code subscribes to
/// AnimationHitboxTrack without adding damage logic to the detector.
/// Remove this component when a real combat receiver is connected.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimationHitboxContactLogger : MonoBehaviour
{
    [SerializeField]
    private AnimationHitboxTrack track;

    [SerializeField]
    private bool logWindowState;

    private void Reset()
    {
        track = GetComponent<AnimationHitboxTrack>();
    }

    private void Awake()
    {
        if (track == null)
            track = GetComponent<AnimationHitboxTrack>();
    }

    private void OnEnable()
    {
        if (track == null)
            return;

        track.ContactDetected += HandleContactDetected;
        track.DetectionWindowOpened += HandleWindowOpened;
        track.DetectionWindowClosed += HandleWindowClosed;
    }

    private void OnDisable()
    {
        if (track == null)
            return;

        track.ContactDetected -= HandleContactDetected;
        track.DetectionWindowOpened -= HandleWindowOpened;
        track.DetectionWindowClosed -= HandleWindowClosed;
    }

    private void HandleContactDetected(AnimationHitboxContact contact)
    {
        Debug.Log(
            $"[HITBOX CONTACT] Target={contact.Target.name} | "
                + $"Frame={contact.FrameIndex} | Box={contact.BoxName} | "
                + $"Physics={(contact.IsPhysics2D ? "2D" : "3D")}",
            this
        );
    }

    private void HandleWindowOpened()
    {
        if (logWindowState)
            Debug.Log("[HITBOX WINDOW] Opened", this);
    }

    private void HandleWindowClosed()
    {
        if (logWindowState)
            Debug.Log("[HITBOX WINDOW] Closed", this);
    }
}
