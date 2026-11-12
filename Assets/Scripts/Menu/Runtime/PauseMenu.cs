using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// In-run pause (Pause key, Esc by default): RESUME, OPTIONS, SAVE &amp; QUIT (to title), SAVE &amp; EXIT GAME,
/// with a summary of the run (time, level, kills, stats, weapons and items). Not available while the
/// level-up draft is open or after death.
/// </summary>
public sealed class PauseMenu : MonoBehaviour
{
    private RunController run;
    private PlayerInput input;
    private RectTransform frame;
    private RectTransform root;
    private OptionsPanel options;
    private GameObject previousSelection;

    public bool IsOpen => root != null;

    public static PauseMenu Create(RunController run)
    {
        var go = new GameObject("Pause Menu");
        go.transform.SetParent(run.transform, false);
        var menu = go.AddComponent<PauseMenu>();
        menu.run = run;
        menu.frame = MenuUI.CreateCanvas("Pause UI", 200, go.transform);
        return menu;
    }

    private void Awake()
    {
        input = PlayerInputLifetime.Create();
        input.Motor.Pause.Enable();
    }

    private void Update()
    {
        if (run == null || run.IsGameOver)
            return;

        bool pausePressed = input.Motor.Pause.WasPressedThisFrame();
        if (!IsOpen)
        {
            if (pausePressed && !run.DraftOpen && !GameplayPause.IsPaused)
                Open();
            return;
        }

        if (options != null || Time.frameCount == OptionsPanel.ClosedFrame)
            return;

        if (pausePressed || MenuInput.CancelPressed())
            Close();
    }

    public void Open()
    {
        if (IsOpen)
            return;

        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        GameplayPause.Request(this);
        Build();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        if (options != null)
            Destroy(options.gameObject);
        Destroy(root.gameObject);
        root = null;
        options = null;
        GameplayPause.Release(this);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
    }

    public void ForceClose() => Close();

    private void Build()
    {
        MenuTheme theme = MenuUI.Theme;
        root = MenuUI.Stretch("Pause", frame);
        MenuUI.Panel(MenuUI.Stretch("Backdrop", root), null, theme != null ? theme.backdrop : new Color(0, 0, 0, 0.7f))
            .raycastTarget = true;

        // Left: the pause window
        MenuUI.Window("Pause Window", root, 40f, 70f, 200f, 220f, out RectTransform left);
        MenuUI.Label(left, "PAUSED", 0f, 4f, 176f, 20f, 16, TextAnchor.UpperCenter, theme?.accent, true, true);
        var buttons = new List<Selectable>
        {
            MenuUI.MakeButton(left, "RESUME", 14f, 36f, 148f, 22f, Close, 8),
            MenuUI.MakeButton(left, "OPTIONS", 14f, 64f, 148f, 22f, OpenOptions, 8),
            MenuUI.MakeButton(left, "SAVE & QUIT", 14f, 92f, 148f, 22f, () => run.SaveAndQuit(false), 8),
            MenuUI.MakeButton(left, "SAVE & EXIT GAME", 14f, 120f, 148f, 22f, () => run.SaveAndQuit(true), 8),
        };
        MenuUI.LinkVertical(buttons);
        MenuUI.Label(left, "Progress is kept until you resume.\nDying ends the saved run.", 4f, 154f, 168f, 30f, 8,
            TextAnchor.UpperCenter, theme?.textDim);

        // Right: run summary
        MenuUI.Window("Run Summary", root, 256f, 40f, 344f, 280f, out RectTransform right);
        BuildSummary(right, theme);

        MenuUI.Select(buttons[0]);
    }

    private void BuildSummary(RectTransform panel, MenuTheme theme)
    {
        float w = panel.rect.width;
        CharacterProfile profile = run.Profile;
        MenuUI.Icon(panel, profile != null ? profile.portrait : null, 0f, 0f, 40f);
        MenuUI.Label(panel, profile != null ? profile.displayName.ToUpperInvariant() : run.CharacterId, 48f, 2f, w - 48f, 18f,
            16, color: theme?.accent, heading: true, shadow: true);
        MenuUI.Label(panel, run.Stage != null ? run.Stage.displayName : run.StageScene, 48f, 22f, w - 48f, 12f, 8,
            color: theme?.textDim);

        int level = run.Experience != null ? run.Experience.CurrentLevel : 1;
        MenuUI.Label(panel,
            $"TIME   {MenuUI.FormatTime(run.ElapsedSeconds)}\nLEVEL  {level}\nKILLS  {run.Kills}\nHP     {run.Player.CurrentHealth}/{run.Player.MaxHealth}",
            0f, 50f, 150f, 50f, 8);

        CharacterCombatSystem stats = run.Player.GetComponentInChildren<CharacterCombatSystem>(true);
        if (stats != null)
        {
            MenuUI.Label(panel,
                $"ATK    {stats.AttackPercent:0}%\nSPD    {stats.MovementSpeedPercent:0}%\nCRT    {stats.CriticalHitPercent:0}%\nHASTE  {stats.HastePercent:0}%",
                160f, 50f, w - 160f, 50f, 8);
        }

        float y = 104f;
        MenuUI.Label(panel, "WEAPONS", 0f, y, w, 12f, 8, color: theme?.accent, heading: true);
        y += 13f;
        foreach (WeaponController weapon in run.Player.GetComponentsInChildren<WeaponController>(true))
        {
            // the ultimate's own attack is not a weapon slot
            if (weapon.Data == null || y > 200f || (profile != null && profile.ultimate != null
                                                     && weapon.Data == profile.ultimate.attackWeapon))
                continue;
            string name = weapon.IsEvolved && weapon.ActiveEvolution != null
                ? weapon.ActiveEvolution.displayName
                : weapon.Data.displayName;
            MenuUI.Icon(panel, weapon.ActiveEvolution?.icon != null ? weapon.ActiveEvolution.icon : weapon.Data.icon, 0f, y, 16f);
            MenuUI.Label(panel, $"{name}  <color=#{ColorUtility.ToHtmlStringRGB(theme != null ? theme.highlight : Color.yellow)}>Lv {weapon.DisplayLevel}/{weapon.MaximumLevel}</color>",
                22f, y + 4f, w - 22f, 12f, 8);
            y += 18f;
        }

        y += 4f;
        MenuUI.Label(panel, "ITEMS", 0f, y, w, 12f, 8, color: theme?.accent, heading: true);
        y += 13f;
        bool anyItem = false;
        foreach (LevelUpItemController item in run.Player.GetComponentsInChildren<LevelUpItemController>(true))
        {
            if (item.Data == null || y > 240f)
                continue;
            anyItem = true;
            MenuUI.Icon(panel, item.Data.icon, 0f, y, 16f);
            MenuUI.Label(panel, $"{item.Data.displayName}  Lv {item.DisplayLevel}", 22f, y + 4f, w - 22f, 12f, 8);
            y += 18f;
        }

        if (!anyItem)
            MenuUI.Label(panel, "-", 0f, y, w, 12f, 8, color: theme?.textDim);
    }

    private void OpenOptions()
    {
        GameObject back = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        options = OptionsPanel.Open(frame, () =>
        {
            options = null;
            if (back != null)
                MenuUI.Select(back.GetComponent<Selectable>());
        });
    }

    private void OnDestroy()
    {
        GameplayPause.Release(this);
        PlayerInputLifetime.Release(ref input);
    }
}
