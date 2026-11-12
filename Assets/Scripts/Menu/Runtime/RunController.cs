using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One per stage scene. Before anything else wakes up it activates the chosen character and hides the
/// others (every stage holds all characters). During the run it records each level-up choice; on Resume it
/// replays them and restores level, EXP, health, position, timer and kills. Owns the pause menu and the
/// game over screen.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class RunController : MonoBehaviour
{
    private readonly List<RunSaveData.RecordedChoice> choices = new();
    private LevelUpDraftController draft;
    private PauseMenu pauseMenu;
    private bool recording = true;
    private bool resumedFromSave;

    public static RunController Instance { get; private set; }

    public string CharacterId { get; private set; }
    public PlayerHealth Player { get; private set; }
    public PlayerExperience Experience { get; private set; }
    public GameplayHUD Hud { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool DraftOpen => draft != null && draft.SelectionActive;
    public string StageScene => SceneManager.GetActiveScene().name;

    public CharacterProfile Profile => GameCatalog.Instance != null ? GameCatalog.Instance.FindCharacter(CharacterId) : null;
    public StageProfile Stage => GameCatalog.Instance != null ? GameCatalog.Instance.FindStage(StageScene) : null;

    public float ElapsedSeconds => Hud != null ? Hud.ElapsedTime
        : EnemyPhaseDirector.Instance != null ? EnemyPhaseDirector.Instance.ElapsedSeconds : 0f;

    public int Kills => Hud != null ? Hud.KillCount : 0;

    private void Awake()
    {
        Instance = this;
        SelectCharacter();
    }

    private void SelectCharacter()
    {
        var roots = new List<PlayerHealth>();
        foreach (PlayerHealth health in FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (health.gameObject.scene == gameObject.scene && health.transform.parent == null)
                roots.Add(health);
        }

        // No session (stage opened directly in the Editor): keep the character that is active in the scene.
        string wanted = RunSession.CharacterId;
        PlayerHealth chosen = null;
        foreach (PlayerHealth root in roots)
        {
            if (!string.IsNullOrEmpty(wanted) ? string.Equals(root.name, wanted, System.StringComparison.OrdinalIgnoreCase)
                                              : root.gameObject.activeSelf && chosen == null)
            {
                chosen = root;
            }
        }

        if (chosen == null && roots.Count > 0)
        {
            Debug.LogWarning($"[Run] Character '{wanted}' is not in this stage; keeping the scene's character.", this);
            chosen = roots.Find(r => r.gameObject.activeSelf) ?? roots[0];
        }

        foreach (PlayerHealth root in roots)
            root.gameObject.SetActive(root == chosen);

        if (chosen == null)
            return;

        Player = chosen;
        CharacterId = chosen.name;
        Experience = chosen.GetComponentInChildren<PlayerExperience>(true);
        draft = chosen.GetComponentInChildren<LevelUpDraftController>(true);
        if (draft != null)
            draft.ChoiceApplied += HandleChoiceApplied;
        Player.Died += HandlePlayerDied;
    }

    private void Start()
    {
        Hud = FindFirstObjectByType<GameplayHUD>();
        pauseMenu = PauseMenu.Create(this);

        RunSaveData save = RunSession.ConsumePendingResume();
        if (save != null && string.Equals(save.characterId, CharacterId, System.StringComparison.OrdinalIgnoreCase))
            StartCoroutine(Restore(save));
    }

    private void HandleChoiceApplied(LevelUpChoice choice)
    {
        if (recording && choice != null)
            choices.Add(new RunSaveData.RecordedChoice { key = choice.Key, level = draft.CurrentDraftLevel });
    }

    // ───────────────────────────── save / resume ─────────────────────────────

    private IEnumerator Restore(RunSaveData save)
    {
        // Wait until every Start ran (weapons equipped, HUD bound, phase timeline loaded).
        yield return null;

        recording = false;
        int failed = 0;
        foreach (RunSaveData.RecordedChoice choice in save.choices)
        {
            if (draft != null && draft.TryReplayChoice(choice.key, choice.level))
                choices.Add(choice);
            else
                failed++;
        }

        recording = true;
        if (failed > 0)
            Debug.LogWarning($"[Run] {failed} saved upgrade(s) could not be restored (game data changed?).", this);

        Experience?.SetProgress(save.level, save.experience);
        Player.RestoreHealth(save.health);

        var body = Player.GetComponent<Rigidbody2D>();
        if (body != null)
            body.position = save.position;
        Player.transform.position = save.position;

        Hud?.RestoreProgress(save.elapsedSeconds, save.kills);
        EnemyPhaseDirector.Instance?.SetElapsed(save.elapsedSeconds, skipEliteWaves: true);
        resumedFromSave = true;
        Debug.Log($"[Run] Resumed {CharacterId} at {MenuUI.FormatTime(save.elapsedSeconds)}, Lv {save.level}, " +
                  $"{choices.Count} upgrade(s).", this);
    }

    public RunSaveData BuildSave()
    {
        var body = Player.GetComponent<Rigidbody2D>();
        return new RunSaveData
        {
            characterId = CharacterId,
            stageScene = StageScene,
            elapsedSeconds = ElapsedSeconds,
            kills = Kills,
            level = Experience != null ? Experience.CurrentLevel : 1,
            experience = Experience != null ? Experience.CurrentExperience : 0,
            health = Player.CurrentHealth,
            position = body != null ? body.position : (Vector2)Player.transform.position,
            choices = new List<RunSaveData.RecordedChoice>(choices),
        };
    }

    public void SaveAndQuit(bool exitGame)
    {
        if (IsGameOver || Player == null)
            return;

        RunSaveSystem.Save(BuildSave());
        if (exitGame)
            RunSession.QuitGame();
        else
            RunSession.LoadMainMenu();
    }

    // ───────────────────────────── game over ─────────────────────────────

    private void HandlePlayerDied(PlayerHealth _)
    {
        if (IsGameOver)
            return;

        IsGameOver = true;
        // The saved run this one continued is over; another run's save is left alone.
        if (resumedFromSave)
            RunSaveSystem.Delete();
        StartCoroutine(ShowGameOver());
    }

    private IEnumerator ShowGameOver()
    {
        yield return new WaitForSecondsRealtime(0.8f);
        pauseMenu?.ForceClose();
        GameplayPause.Request(this);
        GameOverScreen.Show(this);
    }

    public void Retry() => RunSession.Retry(CharacterId, StageScene);

    public void NewRun()
    {
        RunSession.OpenCharacterSelect = true;
        RunSession.LoadMainMenu();
    }

    public void BackToTitle() => RunSession.LoadMainMenu();

    private void OnDestroy()
    {
        if (draft != null)
            draft.ChoiceApplied -= HandleChoiceApplied;
        if (Player != null)
            Player.Died -= HandlePlayerDied;
        GameplayPause.Release(this);
        if (Instance == this)
            Instance = null;
    }
}
