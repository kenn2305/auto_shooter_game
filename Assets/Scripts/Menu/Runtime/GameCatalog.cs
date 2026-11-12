using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything the menus show: playable characters, stages and the UI theme. Lives in a Resources folder
/// (Resources/GameCatalog) so every scene can reach it. Created by Tools > Menu > Setup Menus.
/// </summary>
[CreateAssetMenu(fileName = "GameCatalog", menuName = "Game/Game Catalog")]
public sealed class GameCatalog : ScriptableObject
{
    public string gameTitle = "RUIN SURVIVORS";
    public string mainMenuScene = "MainMenu";
    public MenuTheme theme;
    public List<CharacterProfile> characters = new();
    public List<StageProfile> stages = new();

    private static GameCatalog instance;

    public static GameCatalog Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<GameCatalog>("GameCatalog");
            return instance;
        }
    }

    public CharacterProfile FindCharacter(string id)
    {
        foreach (CharacterProfile c in characters)
        {
            if (c != null && string.Equals(c.characterId, id, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    public StageProfile FindStage(string scene)
    {
        foreach (StageProfile s in stages)
        {
            if (s != null && s.sceneName == scene)
                return s;
        }

        return null;
    }
}

/// <summary>A stage the player can pick (one scene each).</summary>
[Serializable]
public sealed class StageProfile
{
    public string sceneName;
    public string displayName;
    [TextArea] public string description;
    public Sprite preview;
}
