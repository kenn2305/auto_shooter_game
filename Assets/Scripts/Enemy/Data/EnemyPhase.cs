using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>One row of EnemyPhaseConfig.csv: a time window of the run and how hard enemies are in it.</summary>
[Serializable]
public sealed class EnemyPhase
{
    public string Name;

    [Tooltip("Run minute when this phase starts.")]
    public float StartMinute;

    [Tooltip("Multiplier on every enemy's base stat (2 = +100%). Stationary enemies are not scaled.")]
    public float HealthMultiplier = 1f;
    public float DamageMultiplier = 1f;
    public float SpeedMultiplier = 1f;

    [Tooltip("Extra HP and ATK in percent of the phase multiplier, per minute spent in the phase " +
        "(10 = +10% per minute). Keeps enemies scaling inside long phases.")]
    public float GrowthPerMinute;

    [Tooltip("Normal enemies spawned per second.")]
    public float SpawnsPerSecond = 1f;

    [Tooltip("Cap of normal enemies alive at once.")]
    public int MaxAlive = 50;

    [Tooltip("Elites per wave.")]
    public int EliteCount;

    [Tooltip("0 = one elite wave when the phase ends. >0 = one wave every N minutes during the phase.")]
    public float EliteEveryMinutes;

    public float StartSeconds => StartMinute * 60f;

    /// <summary>Parses EnemyPhaseConfig.csv. Lines starting with # are comments; the first other line is the header.</summary>
    public static List<EnemyPhase> ParseCsv(string text, out string error)
    {
        error = null;
        List<EnemyPhase> phases = new List<EnemyPhase>();
        string[] header = null;
        string[] lines = text.TrimStart('﻿').Split('\n');

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            string[] cells = line.Split(',').Select(c => c.Trim()).ToArray();
            if (cells.All(c => c.Length == 0))
                continue;

            if (header == null)
            {
                header = cells;
                continue;
            }

            try
            {
                phases.Add(ParseRow(header, cells));
            }
            catch (Exception exception)
            {
                error = $"EnemyPhaseConfig.csv line {lineIndex + 1}: {exception.Message}";
                return null;
            }
        }

        if (phases.Count == 0)
        {
            error = "EnemyPhaseConfig.csv has no phase rows.";
            return null;
        }

        phases.Sort((a, b) => a.StartMinute.CompareTo(b.StartMinute));
        return phases;
    }

    private static EnemyPhase ParseRow(string[] header, string[] cells)
    {
        string Get(string column, string fallback = null)
        {
            int index = Array.FindIndex(header, h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index < cells.Length && cells[index].Length > 0)
                return cells[index];
            if (fallback != null)
                return fallback;
            throw new FormatException($"'{column}' is missing or empty");
        }

        float Float(string column, string fallback = null) =>
            float.Parse(Get(column, fallback), NumberStyles.Float, CultureInfo.InvariantCulture);

        return new EnemyPhase
        {
            Name = Get("Phase"),
            StartMinute = Float("StartMinute"),
            HealthMultiplier = Mathf.Max(0.01f, Float("HP", "1")),
            DamageMultiplier = Mathf.Max(0.01f, Float("ATK", "1")),
            SpeedMultiplier = Mathf.Max(0f, Float("SPD", "1")),
            GrowthPerMinute = Mathf.Max(0f, Float("Growth", "0")),
            SpawnsPerSecond = Mathf.Max(0f, Float("SpawnPerSecond")),
            MaxAlive = Mathf.Max(0, Mathf.RoundToInt(Float("MaxAlive"))),
            EliteCount = Mathf.Max(0, Mathf.RoundToInt(Float("EliteCount", "0"))),
            EliteEveryMinutes = Mathf.Max(0f, Float("EliteEveryMinutes", "0")),
        };
    }
}
