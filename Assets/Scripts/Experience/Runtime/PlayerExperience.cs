using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerExperience : MonoBehaviour
{
    [SerializeField] private ExperienceSystemConfig config;
    [SerializeField] private bool resetProgressOnAwake = true;

    [Header("Runtime Debug")]
    [SerializeField, Min(1)] private int currentLevel = 1;
    [SerializeField, Min(0)] private int currentExperience;
    [SerializeField, Min(1)] private int experienceRequired = 79;

    [SerializeField, Min(0)]
    [Tooltip("Total EXP collected through AddExperience during the current Play session.")]
    private long sessionExperienceCollected;
    [SerializeField, Min(0)] private int lastExperienceGain;
    [SerializeField, Min(0)] private int levelsGainedFromLastGrant;

    [SerializeField]
    [Tooltip("When enabled, every EXP grant prints a concise progress line to the Console.")]
    private bool logExperienceChanges;

    private bool ownsRuntimeConfig;

    public event Action<int, int, int> ExperienceChanged;
    public event Action<int> LevelGained;

    public int CurrentLevel => currentLevel;
    public int CurrentExperience => currentExperience;
    public int ExperienceRequired => experienceRequired;
    public int ExperienceRemaining => Mathf.Max(0, experienceRequired - currentExperience);
    public long SessionExperienceCollected => sessionExperienceCollected;
    public int LastExperienceGain => lastExperienceGain;
    public int LevelsGainedFromLastGrant => levelsGainedFromLastGrant;
    public float NormalizedProgress => experienceRequired > 0 ? currentExperience / (float)experienceRequired : 0f;
    public float ProgressPercent => NormalizedProgress * 100f;
    public string DebugSummary =>
        $"Level {currentLevel} | EXP {currentExperience}/{experienceRequired} "
        + $"({ProgressPercent:0.0}%) | Remaining {ExperienceRemaining}";

    private void Awake()
    {
        ResolveConfig();
        if (resetProgressOnAwake)
        {
            currentLevel = 1;
            currentExperience = 0;
        }

        sessionExperienceCollected = 0;
        lastExperienceGain = 0;
        levelsGainedFromLastGrant = 0;
        currentLevel = Mathf.Max(1, currentLevel);
        experienceRequired = config.GetExperienceRequired(currentLevel);
        currentExperience = Mathf.Clamp(currentExperience, 0, experienceRequired - 1);
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        ResolveConfig();
        lastExperienceGain = amount;
        levelsGainedFromLastGrant = 0;
        sessionExperienceCollected += amount;
        if (experienceRequired <= 0)
        {
            currentLevel = Mathf.Max(1, currentLevel);
            experienceRequired = config.GetExperienceRequired(currentLevel);
        }

        long pendingExperience = (long)currentExperience + amount;
        while (pendingExperience >= experienceRequired)
        {
            pendingExperience -= experienceRequired;
            currentLevel++;
            levelsGainedFromLastGrant++;
            experienceRequired = config.GetExperienceRequired(currentLevel);

            // Character stats are intentionally untouched. A player level only requests
            // a future equipment/upgrade choice through this event.
            LevelGained?.Invoke(currentLevel);
        }

        currentExperience = (int)Math.Min(pendingExperience, int.MaxValue);
        ExperienceChanged?.Invoke(currentLevel, currentExperience, experienceRequired);

        if (logExperienceChanges)
        {
            Debug.Log(
                $"[EXP] +{amount} => {DebugSummary} | "
                    + $"Levels gained: {levelsGainedFromLastGrant} | "
                    + $"Session total: {sessionExperienceCollected}",
                this
            );
        }
    }

    public void SetProgress(int level, int experience)
    {
        ResolveConfig();
        currentLevel = Mathf.Max(1, level);
        experienceRequired = config.GetExperienceRequired(currentLevel);
        currentExperience = Mathf.Clamp(experience, 0, experienceRequired - 1);
        lastExperienceGain = 0;
        levelsGainedFromLastGrant = 0;
        ExperienceChanged?.Invoke(currentLevel, currentExperience, experienceRequired);
    }

    [ContextMenu("Debug/Log Current EXP State")]
    private void LogCurrentExperienceState()
    {
        Debug.Log($"[EXP] {DebugSummary} | Session total: {sessionExperienceCollected}", this);
    }

    private void OnDestroy()
    {
        if (ownsRuntimeConfig && config != null)
        {
            Destroy(config);
        }
    }

    private void ResolveConfig()
    {
        if (config == null)
        {
            config = Resources.Load<ExperienceSystemConfig>(ExperienceSystemConfig.ResourceName);
        }

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ExperienceSystemConfig>();
            ownsRuntimeConfig = true;
            Debug.LogWarning(
                "ExperienceSystemConfig was not found in Resources; using runtime defaults.",
                this
            );
        }
    }
}
