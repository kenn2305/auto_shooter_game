using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Frame-time benchmark for player builds. Does nothing unless the game is started with
/// <c>-perfprobe</c> on the command line:
///   Game.exe -perfprobe [-perfstress] [-perfwarmup 10] [-perfseconds 30]
/// It uncaps the frame rate (vSync off), waits for the warm-up, records every frame for the given
/// duration, writes a summary to the player log and to &lt;persistentDataPath&gt;/perf_probe.txt, then quits.
/// <c>-perfstress</c> spawns enemies as fast as the spawn settings allow (up to maxAlive), makes the
/// player unkillable and skips the level-up draft, so the run measures a full crowd.
/// </summary>
public sealed class PerfProbe : MonoBehaviour
{
    private float warmup = 10f;
    private float duration = 30f;
    private bool stress;

    private readonly List<float> frameMs = new(8192);
    private float startTime;
    private int maxEnemies;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(args, "-perfprobe") < 0)
            return;

        var probe = new GameObject("[PerfProbe]").AddComponent<PerfProbe>();
        DontDestroyOnLoad(probe.gameObject);
        probe.stress = System.Array.IndexOf(args, "-perfstress") >= 0;
        probe.warmup = ReadFloat(args, "-perfwarmup", probe.warmup);
        probe.duration = ReadFloat(args, "-perfseconds", probe.duration);
    }

    private static float ReadFloat(string[] args, string key, float fallback)
    {
        int i = System.Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }

    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        startTime = Time.realtimeSinceStartup;

        if (stress)
            ApplyStress();
    }

    private static void ApplyStress()
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        var spawner = FindFirstObjectByType<EnemySpawnManager>();
        FieldInfo settingsField = typeof(EnemySpawnManager).GetField("settings", Private);
        if (spawner != null && settingsField?.GetValue(spawner) is EnemySpawnSetting settings)
        {
            EnemySpawnSetting fast = Instantiate(settings);
            fast.spawnInterval = 0.05f;
            fast.maxSpawnPerFrame = 8;
            settingsField.SetValue(spawner, fast);
        }

        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            typeof(PlayerHealth).GetField("maxHealth", Private)?.SetValue(health, 1_000_000);
            typeof(PlayerHealth).GetField("currentHealth", Private)?.SetValue(health, 1_000_000);
        }

        var draft = FindFirstObjectByType<LevelUpDraftController>();
        if (draft != null)
            draft.enabled = false;
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            Time.timeScale = 1f; // a level-up draft that slipped through must not freeze the benchmark

        float elapsed = Time.realtimeSinceStartup - startTime;
        if (elapsed < warmup)
            return;

        frameMs.Add(Time.unscaledDeltaTime * 1000f);
        if (EnemyCrowdManager.Instance != null)
            maxEnemies = Mathf.Max(maxEnemies, EnemyCrowdManager.Instance.ActiveAgentCount);

        if (elapsed >= warmup + duration)
            Finish();
    }

    private void Finish()
    {
        enabled = false;
        var sorted = new List<float>(frameMs);
        sorted.Sort();
        int n = sorted.Count;
        float total = 0f;
        int over120 = 0;
        int over60 = 0;
        foreach (float ms in sorted)
        {
            total += ms;
            if (ms > 1000f / 120f) over120++;
            if (ms > 1000f / 60f) over60++;
        }

        var report = new StringBuilder();
        report.AppendLine($"[PerfProbe] {Application.productName} {Application.version} | {SystemInfo.processorType} | {SystemInfo.graphicsDeviceName}");
        report.AppendLine($"[PerfProbe] stress={stress} frames={n} seconds={duration} maxEnemies={maxEnemies}");
        report.AppendLine($"[PerfProbe] avgFPS={n / (total / 1000f):F1} frameMs median={sorted[n / 2]:F2} p95={sorted[(int)(n * 0.95f)]:F2} " +
            $"p99={sorted[(int)(n * 0.99f)]:F2} max={sorted[n - 1]:F2}");
        report.AppendLine($"[PerfProbe] frames slower than 120fps={over120} ({100f * over120 / n:F1}%), slower than 60fps={over60} ({100f * over60 / n:F1}%)");

        Debug.Log(report.ToString());
        File.WriteAllText(Path.Combine(Application.persistentDataPath, "perf_probe.txt"), report.ToString());
        Application.Quit();
    }
}
