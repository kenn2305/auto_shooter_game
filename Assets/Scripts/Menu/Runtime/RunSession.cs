using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// What the next stage scene should play: chosen character, and a save to restore when resuming.
/// Survives scene loads (static). Stage scenes opened directly in the Editor have no session and keep the
/// character that is active in the scene.
/// </summary>
public static class RunSession
{
    public static string CharacterId { get; private set; }
    public static string StageScene { get; private set; }
    /// <summary>Set by Resume; consumed by RunController once the stage is loaded.</summary>
    public static RunSaveData PendingResume { get; private set; }
    /// <summary>The title screen opens straight on character select (Game Over > New Run).</summary>
    public static bool OpenCharacterSelect { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        CharacterId = null;
        StageScene = null;
        PendingResume = null;
        OpenCharacterSelect = false;
    }

    public static void StartNewRun(string characterId, string stageScene)
    {
        CharacterId = characterId;
        StageScene = stageScene;
        PendingResume = null;
        LoadScene(stageScene);
    }

    public static bool ResumeSavedRun()
    {
        RunSaveData save = RunSaveSystem.Load();
        if (save == null || !Application.CanStreamedLevelBeLoaded(save.stageScene))
            return false;

        CharacterId = save.characterId;
        StageScene = save.stageScene;
        PendingResume = save;
        LoadScene(save.stageScene);
        return true;
    }

    /// <summary>Same character and stage, from the beginning (not from the save).</summary>
    public static void Retry(string characterId, string stageScene)
    {
        StartNewRun(characterId, stageScene);
    }

    public static RunSaveData ConsumePendingResume()
    {
        RunSaveData save = PendingResume;
        PendingResume = null;
        return save;
    }

    public static void LoadMainMenu()
    {
        PendingResume = null;
        LoadScene(GameCatalog.Instance != null ? GameCatalog.Instance.mainMenuScene : "MainMenu");
    }

    public static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void LoadScene(string scene)
    {
        // A paused stage leaves Time.timeScale at 0; GameplayPause resets with the domain only.
        GameplayPause.ReleaseAll();
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }
}
