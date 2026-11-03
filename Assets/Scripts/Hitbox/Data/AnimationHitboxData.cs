using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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
    [SerializeField] private int frameIndex;
    [SerializeField] private List<HitboxData> hitboxes = new();

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
