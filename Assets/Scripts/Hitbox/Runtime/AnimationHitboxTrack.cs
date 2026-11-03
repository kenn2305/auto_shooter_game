using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[Serializable]
public class HitboxData
{
    public string boxName = "Hitbox";
    public bool enabled = true;
    public Vector2 offset;
    public Vector2 size = Vector2.one;
}

[Serializable]
public class AnimationHitboxFrameData
{
    [SerializeField]
    private int frameIndex;

    [SerializeField]
    private List<HitboxData> hitboxes = new();

    public int FrameIndex => frameIndex;
    public List<HitboxData> Hitboxes => hitboxes;

    public AnimationHitboxFrameData(int index)
    {
        frameIndex = index;
    }

    public void SetFrameIndex(int index)
    {
        frameIndex = index;
    }
}

public enum HitboxActivationMode
{
    FramesWithHitboxes,
    AnimationEvents,
}

public enum HitboxDetectionMode
{
    Both,
    Physics2D,
    Physics3D,
}

/// <summary>
/// Information produced by AnimationHitboxTrack when one target overlaps an
/// enabled hitbox. This is detection data only; it deliberately contains no
/// damage or gameplay result.
/// </summary>
public readonly struct AnimationHitboxContact
{
    public AnimationHitboxTrack Source { get; }
    public GameObject Target { get; }
    public Component Collider { get; }
    public int FrameIndex { get; }
    public string BoxName { get; }
    public Vector2 WorldCenter { get; }
    public Vector2 WorldSize { get; }
    public bool IsPhysics2D { get; }

    public AnimationHitboxContact(
        AnimationHitboxTrack source,
        GameObject target,
        Component collider,
        int frameIndex,
        string boxName,
        Vector2 worldCenter,
        Vector2 worldSize,
        bool isPhysics2D
    )
    {
        Source = source;
        Target = target;
        Collider = collider;
        FrameIndex = frameIndex;
        BoxName = boxName;
        WorldCenter = worldCenter;
        WorldSize = worldSize;
        IsPhysics2D = isPhysics2D;
    }
}

[Serializable]
public sealed class HitboxTargetUnityEvent : UnityEvent<GameObject> { }

[DisallowMultipleComponent]
public class AnimationHitboxTrack : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AnimationClip animationClip;

    [Header("Detection")]
    [FormerlySerializedAs("enemyLayer")]
    [SerializeField]
    private LayerMask targetLayer;

    [SerializeField]
    private HitboxDetectionMode detectionMode = HitboxDetectionMode.Physics2D;

    [SerializeField, Min(0.01f)]
    private float detectionDepth = 2f;

    [Header("Detection Signals")]
    [Tooltip(
        "Invoked once per detected target per physics scan. "
            + "Use ContactDetected from code when frame/box/collider details are needed."
    )]
    [SerializeField]
    private HitboxTargetUnityEvent onTargetDetected = new();

    [Header("Activation")]
    [SerializeField]
    private HitboxActivationMode activationMode = HitboxActivationMode.FramesWithHitboxes;

    [Header("Frames")]
    [SerializeField]
    private List<AnimationHitboxFrameData> frames = new();

    [Header("Scene Preview")]
    [SerializeField]
    private bool showHitboxesInScene = true;

    private bool animationEventActive;
    private bool runtimeWindowActive;
    private int previousRuntimeFrame = -1;
    private float runtimeSizeMultiplier = 1f;
    private readonly HashSet<GameObject> targetsDetectedThisScan = new();

    [SerializeField, HideInInspector]
    private int editorPreviewFrame;

    [SerializeField, HideInInspector]
    private int editorSelectedBox = -1;

    [NonSerialized]
    private int runtimeFrame = -1;

    public Animator Animator => animator;
    public AnimationClip AnimationClip => animationClip;
    public IReadOnlyList<AnimationHitboxFrameData> Frames => frames;
    public HitboxTargetUnityEvent OnTargetDetected => onTargetDetected;
    public float RuntimeSizeMultiplier => runtimeSizeMultiplier;

    /// <summary>
    /// Raw detection signal. It can fire again for the same target on a later
    /// physics scan. Subscribers decide one-hit, cooldown and damage rules.
    /// </summary>
    public event Action<AnimationHitboxContact> ContactDetected;

    /// <summary>
    /// Raised when a contiguous animation hitbox window becomes active.
    /// </summary>
    public event Action DetectionWindowOpened;

    /// <summary>
    /// Raised when the current animation hitbox window closes.
    /// </summary>
    public event Action DetectionWindowClosed;

    /// <summary>
    /// Applies runtime detection settings supplied by the attack/weapon layer.
    /// It changes only which targets are queried and the size of the query.
    /// </summary>
    public void ConfigureDetection(LayerMask targets, float sizeMultiplier = 1f)
    {
        targetLayer = targets;
        runtimeSizeMultiplier = Mathf.Max(0f, sizeMultiplier);
    }

    public int ExpectedFrameCount
    {
        get
        {
            if (animationClip == null || animationClip.frameRate <= 0f)
                return 0;

            return Mathf.Max(1, Mathf.RoundToInt(animationClip.length * animationClip.frameRate));
        }
    }

    private void Reset()
    {
        animator = GetComponent<Animator>();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

            if (clips.Length > 0)
                animationClip = clips[0];
        }

        SyncFramesToClip();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void OnDisable()
    {
        animationEventActive = false;
        previousRuntimeFrame = -1;
        DeactivateRuntimeWindow();
    }

    private void FixedUpdate()
    {
        if (!TryGetCurrentFrame(out int frameIndex))
        {
            DeactivateRuntimeWindow();
            return;
        }

        AnimationHitboxFrameData frame = frames[frameIndex];

        bool shouldBeActive =
            activationMode == HitboxActivationMode.AnimationEvents
                ? animationEventActive
                : HasEnabledHitbox(frame);

        if (!shouldBeActive)
        {
            DeactivateRuntimeWindow();
            previousRuntimeFrame = frameIndex;
            return;
        }

        bool animationLooped = previousRuntimeFrame >= 0 && frameIndex < previousRuntimeFrame;

        if (animationLooped && runtimeWindowActive)
            DeactivateRuntimeWindow();

        ActivateRuntimeWindow();

        runtimeFrame = frameIndex;
        previousRuntimeFrame = frameIndex;
        DetectContacts(frame);
    }

    public void EnableHitboxes()
    {
        animationEventActive = true;
    }

    public void DisableHitboxes()
    {
        animationEventActive = false;
        DeactivateRuntimeWindow();
    }

    private static bool HasEnabledHitbox(AnimationHitboxFrameData frame)
    {
        foreach (HitboxData box in frame.Hitboxes)
        {
            if (box.enabled)
                return true;
        }

        return false;
    }

    private void DeactivateRuntimeWindow()
    {
        if (!runtimeWindowActive)
        {
            runtimeFrame = -1;
            return;
        }

        runtimeWindowActive = false;
        runtimeFrame = -1;
        targetsDetectedThisScan.Clear();
        DetectionWindowClosed?.Invoke();
    }

    private void ActivateRuntimeWindow()
    {
        if (runtimeWindowActive)
            return;

        runtimeWindowActive = true;
        targetsDetectedThisScan.Clear();
        DetectionWindowOpened?.Invoke();
    }

    public void SetEditorPreview(int frameIndex, int boxIndex)
    {
        editorPreviewFrame = frameIndex;
        editorSelectedBox = boxIndex;
    }

    public void SyncFramesToClip()
    {
        int requiredFrameCount = ExpectedFrameCount;

        while (frames.Count < requiredFrameCount)
        {
            frames.Add(new AnimationHitboxFrameData(frames.Count));
        }

        if (frames.Count > requiredFrameCount)
        {
            frames.RemoveRange(requiredFrameCount, frames.Count - requiredFrameCount);
        }

        for (int i = 0; i < frames.Count; i++)
            frames[i].SetFrameIndex(i);
    }

    public AnimationHitboxFrameData GetFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= frames.Count)
            return null;

        return frames[frameIndex];
    }

    public void CopyFrame(int sourceFrame, int destinationFrame)
    {
        AnimationHitboxFrameData source = GetFrame(sourceFrame);
        AnimationHitboxFrameData destination = GetFrame(destinationFrame);

        if (source == null || destination == null)
            return;

        destination.Hitboxes.Clear();

        foreach (HitboxData sourceBox in source.Hitboxes)
        {
            destination.Hitboxes.Add(
                new HitboxData
                {
                    boxName = sourceBox.boxName,
                    enabled = sourceBox.enabled,
                    offset = sourceBox.offset,
                    size = sourceBox.size,
                }
            );
        }
    }

    public void ClearFrame(int frameIndex)
    {
        GetFrame(frameIndex)?.Hitboxes.Clear();
    }

    private bool TryGetCurrentFrame(out int frameIndex)
    {
        frameIndex = -1;

        if (animator == null || animationClip == null || frames.Count == 0)
        {
            return false;
        }

        AnimatorClipInfo[] playingClips = animator.GetCurrentAnimatorClipInfo(0);

        bool clipIsPlaying = false;

        foreach (AnimatorClipInfo clipInfo in playingClips)
        {
            if (clipInfo.clip == animationClip)
            {
                clipIsPlaying = true;
                break;
            }
        }

        if (!clipIsPlaying)
            return false;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        float normalizedTime = animationClip.isLooping
            ? Mathf.Repeat(state.normalizedTime, 1f)
            : Mathf.Clamp01(state.normalizedTime);

        frameIndex = Mathf.Clamp(
            Mathf.FloorToInt(normalizedTime * frames.Count),
            0,
            frames.Count - 1
        );

        return true;
    }

    private void DetectContacts(AnimationHitboxFrameData frame)
    {
        // Several boxes or colliders can report the same target during one
        // FixedUpdate. Emit one contact per target for this scan, then allow
        // the target to be reported again on the next scan. Gameplay code is
        // responsible for one-hit and cooldown policies.
        targetsDetectedThisScan.Clear();

        foreach (HitboxData box in frame.Hitboxes)
        {
            if (!box.enabled)
                continue;

            Vector2 worldCenter = transform.TransformPoint(box.offset);

            Vector3 scale = transform.lossyScale;

            Vector2 worldSize = new Vector2(
                box.size.x * Mathf.Abs(scale.x) * runtimeSizeMultiplier,
                box.size.y * Mathf.Abs(scale.y) * runtimeSizeMultiplier
            );

            if (
                detectionMode == HitboxDetectionMode.Both
                || detectionMode == HitboxDetectionMode.Physics2D
            )
            {
                DetectPhysics2D(frame, box, worldCenter, worldSize);
            }

            if (
                detectionMode == HitboxDetectionMode.Both
                || detectionMode == HitboxDetectionMode.Physics3D
            )
            {
                DetectPhysics3D(frame, box, worldCenter, worldSize, scale);
            }
        }
    }

    private void DetectPhysics2D(
        AnimationHitboxFrameData frame,
        HitboxData box,
        Vector2 worldCenter,
        Vector2 worldSize
    )
    {
        Collider2D[] results = Physics2D.OverlapBoxAll(
            worldCenter,
            worldSize,
            transform.eulerAngles.z,
            targetLayer
        );

        foreach (Collider2D result in results)
        {
            GameObject enemy =
                result.attachedRigidbody != null
                    ? result.attachedRigidbody.gameObject
                    : result.gameObject;

            ReportDetection(frame, box, enemy, result, worldCenter, worldSize, true);
        }
    }

    private void DetectPhysics3D(
        AnimationHitboxFrameData frame,
        HitboxData box,
        Vector2 worldCenter,
        Vector2 worldSize,
        Vector3 scale
    )
    {
        float worldDepth = Mathf.Max(0.01f, detectionDepth * Mathf.Abs(scale.z));

        Collider[] results = Physics.OverlapBox(
            new Vector3(worldCenter.x, worldCenter.y, transform.position.z),
            new Vector3(worldSize.x * 0.5f, worldSize.y * 0.5f, worldDepth * 0.5f),
            transform.rotation,
            targetLayer,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider result in results)
        {
            GameObject enemy =
                result.attachedRigidbody != null
                    ? result.attachedRigidbody.gameObject
                    : result.gameObject;

            ReportDetection(frame, box, enemy, result, worldCenter, worldSize, false);
        }
    }

    private void ReportDetection(
        AnimationHitboxFrameData frame,
        HitboxData box,
        GameObject target,
        Component collider,
        Vector2 worldCenter,
        Vector2 worldSize,
        bool isPhysics2D
    )
    {
        if (target == null || !targetsDetectedThisScan.Add(target))
            return;

        AnimationHitboxContact contact = new AnimationHitboxContact(
            this,
            target,
            collider,
            frame.FrameIndex,
            box.boxName,
            worldCenter,
            worldSize,
            isPhysics2D
        );

        ContactDetected?.Invoke(contact);
        onTargetDetected?.Invoke(target);
    }

    private void OnDrawGizmos()
    {
        if (!showHitboxesInScene || frames == null || frames.Count == 0)
            return;

        int frameIndex = Application.isPlaying ? runtimeFrame : editorPreviewFrame;

        if (Application.isPlaying && !runtimeWindowActive)
            return;

        if (frameIndex < 0 || frameIndex >= frames.Count)
            return;

        AnimationHitboxFrameData frame = frames[frameIndex];

        Gizmos.matrix = transform.localToWorldMatrix;

        for (int i = 0; i < frame.Hitboxes.Count; i++)
        {
            HitboxData box = frame.Hitboxes[i];

            if (!box.enabled)
                continue;

            if (Application.isPlaying)
                Gizmos.color = Color.red;
            else if (i == editorSelectedBox)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.cyan;

            Gizmos.DrawWireCube(box.offset, box.size);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}
