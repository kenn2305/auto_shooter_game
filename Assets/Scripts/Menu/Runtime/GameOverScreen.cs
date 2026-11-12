using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shown when the character dies: run results and RETRY (same character and stage, from the start),
/// NEW RUN (character select), TITLE.
/// </summary>
public sealed class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Show(RunController run)
    {
        var go = new GameObject("Game Over");
        go.transform.SetParent(run.transform, false);
        var screen = go.AddComponent<GameOverScreen>();
        screen.Build(run);
        return screen;
    }

    private void Build(RunController run)
    {
        MenuTheme theme = MenuUI.Theme;
        RectTransform frame = MenuUI.CreateCanvas("Game Over UI", 300, transform);
        MenuUI.Panel(MenuUI.Stretch("Backdrop", frame), null, new Color(0.16f, 0.03f, 0.03f, 0.9f)).raycastTarget = true;

        MenuUI.Label(frame, "GAME OVER", 0f, 40f, 640f, 40f, 32, TextAnchor.UpperCenter,
            theme != null ? theme.danger : Color.red, heading: true, shadow: true);

        MenuUI.Window("Results", frame, 180f, 96f, 280f, 132f, out RectTransform results);
        float w = results.rect.width;
        CharacterProfile profile = run.Profile;
        MenuUI.Icon(results, profile != null ? profile.portrait : null, 0f, 0f, 40f);
        MenuUI.Label(results, profile != null ? profile.displayName.ToUpperInvariant() : run.CharacterId, 48f, 2f,
            w - 48f, 18f, 16, color: theme?.accent, heading: true, shadow: true);
        MenuUI.Label(results, run.Stage != null ? run.Stage.displayName : run.StageScene, 48f, 22f, w - 48f, 12f, 8,
            color: theme?.textDim);

        int level = run.Experience != null ? run.Experience.CurrentLevel : 1;
        MenuUI.Label(results, $"TIME SURVIVED\nLEVEL REACHED\nENEMIES DEFEATED", 8f, 50f, 140f, 40f, 8);
        MenuUI.Label(results, $"{MenuUI.FormatTime(run.ElapsedSeconds)}\n{level}\n{run.Kills}", w - 100f, 50f, 92f, 40f, 8,
            TextAnchor.UpperRight, theme?.highlight);

        var buttons = new List<Selectable>
        {
            MenuUI.MakeButton(frame, "RETRY", 230f, 244f, 180f, 22f, run.Retry, 8),
            MenuUI.MakeButton(frame, "NEW RUN", 230f, 272f, 180f, 22f, run.NewRun, 8),
            MenuUI.MakeButton(frame, "TITLE", 230f, 300f, 180f, 22f, run.BackToTitle, 8),
        };
        MenuUI.LinkVertical(buttons);
        MenuUI.Select(buttons[0]);
    }
}
