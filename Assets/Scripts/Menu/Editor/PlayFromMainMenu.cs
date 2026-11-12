using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Pressing Play in the editor always starts the game at the main menu (like the built game), whatever
/// scene is open. Toggle with Tools > Menu > Play From Main Menu to test a stage scene directly.
/// </summary>
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    private const string MenuPath = "Tools/Menu/Play From Main Menu";
    private const string PrefKey = "PrototypeAutoShooter.PlayFromMainMenu";
    private const string MainMenuScene = "Assets/Scenes/MainMenu.unity";

    static PlayFromMainMenu()
    {
        EditorApplication.delayCall += Apply;
    }

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath, priority = 200)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScene)
            : null;
    }
}
