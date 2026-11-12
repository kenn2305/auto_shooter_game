using UnityEngine;

/// <summary>
/// Options that are not key bindings: volume, fullscreen, VSync, damage numbers. Stored in PlayerPrefs
/// and applied before the first scene loads.
/// </summary>
public static class GameSettings
{
    private const string VolumeKey = "settings.masterVolume";
    private const string FullscreenKey = "settings.fullscreen";
    private const string VSyncKey = "settings.vsync";

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
            AudioListener.volume = MasterVolume;
        }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            ApplyFullscreen();
        }
    }

    public static bool VSync
    {
        get => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
            QualitySettings.vSyncCount = value ? 1 : 0;
        }
    }

    public static void Save() => PlayerPrefs.Save();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyAll()
    {
        AudioListener.volume = MasterVolume;
        // PerfProbe turns VSync off for benchmarks; it runs after this, so it still wins.
        QualitySettings.vSyncCount = VSync ? 1 : 0;
#if !UNITY_EDITOR
        ApplyFullscreen();
#endif
    }

    private static void ApplyFullscreen()
    {
#if !UNITY_EDITOR
        bool full = Fullscreen;
        if (full)
        {
            Resolution native = Screen.currentResolution;
            Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }
#endif
    }
}
