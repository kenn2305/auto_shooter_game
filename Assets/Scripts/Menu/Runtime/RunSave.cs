using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Snapshot of a run taken by "Save &amp; Quit" in the pause menu.</summary>
[Serializable]
public sealed class RunSaveData
{
    public int version = 1;
    public string characterId;
    public string stageScene;
    public float elapsedSeconds;
    public int kills;
    public int level = 1;
    public int experience;
    public int health;
    public Vector2 position;
    /// <summary>Every level-up choice in the order it was taken, replayed on resume.</summary>
    public List<RecordedChoice> choices = new();
    public string savedAt;

    [Serializable]
    public struct RecordedChoice
    {
        public string key;
        public int level;
    }
}

/// <summary>One save slot on disk (persistentDataPath/run_save.json).</summary>
public static class RunSaveSystem
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "run_save.json");

    public static bool HasSave => File.Exists(FilePath);

    public static void Save(RunSaveData data)
    {
        data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[RunSave] Could not write {FilePath}: {e.Message}");
        }
    }

    public static RunSaveData Load()
    {
        if (!HasSave)
            return null;

        try
        {
            var data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath));
            return data != null && !string.IsNullOrEmpty(data.characterId) && !string.IsNullOrEmpty(data.stageScene)
                ? data
                : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RunSave] Save file is unreadable and was ignored: {e.Message}");
            return null;
        }
    }

    public static void Delete()
    {
        if (HasSave)
            File.Delete(FilePath);
    }
}
