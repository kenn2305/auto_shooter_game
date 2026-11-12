using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single place that pauses gameplay: while any owner holds a pause request, Time.timeScale is 0 and
/// gameplay input (movement, aiming, ultimate) is ignored. UI input keeps working, so menus such as the
/// level-up draft can still be used. Several owners may pause at once; time resumes when the last releases.
/// </summary>
public static class GameplayPause
{
    private static readonly HashSet<object> Owners = new();
    private static float timeScaleBeforePause = 1f;

    /// <summary>Raised with true when gameplay pauses and false when it resumes.</summary>
    public static event Action<bool> Changed;

    public static bool IsPaused => Owners.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Owners.Clear();
        timeScaleBeforePause = 1f;
        Changed = null;
    }

    public static void Request(object owner)
    {
        if (owner == null || !Owners.Add(owner) || Owners.Count != 1)
        {
            return;
        }

        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        Changed?.Invoke(true);
    }

    public static void Release(object owner)
    {
        if (owner == null || !Owners.Remove(owner) || Owners.Count != 0)
        {
            return;
        }

        Time.timeScale = timeScaleBeforePause;
        Changed?.Invoke(false);
    }
}
