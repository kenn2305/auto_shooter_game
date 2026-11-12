using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title scene: title screen (Start / Resume / Options / Quit) → character select → stage select.
/// The whole UI is built from the <see cref="GameCatalog"/> and its <see cref="MenuTheme"/>.
/// </summary>
public sealed class MainMenuController : MonoBehaviour
{
    private enum Page { Title, Characters, Stages }

    private RectTransform frame;
    private RectTransform page;
    private Page current;
    private OptionsPanel options;
    private CharacterProfile chosenCharacter;
    private int characterIndex;
    private int stageIndex;

    // character select widgets refreshed on selection
    private Image previewImage;
    private MenuSpriteAnimator previewAnimator;
    private Text nameText;
    private Text epithetText;
    private Text statsText;
    private RectTransform infoContent;
    private RectTransform selectionFrame;

    private GameCatalog Catalog => GameCatalog.Instance;
    private MenuTheme Theme => MenuUI.Theme;

    private void Start()
    {
        GameplayPause.ReleaseAll();
        Time.timeScale = 1f;
        frame = MenuUI.CreateCanvas("Main Menu UI", 100, transform);

        if (RunSession.OpenCharacterSelect)
        {
            RunSession.OpenCharacterSelect = false;
            ShowCharacters();
        }
        else
        {
            ShowTitle();
        }
    }

    private void Update()
    {
        if (options != null || Time.frameCount == OptionsPanel.ClosedFrame || !MenuInput.CancelPressed())
            return;

        if (current == Page.Stages)
            ShowCharacters();
        else if (current == Page.Characters)
            ShowTitle();
    }

    private RectTransform NewPage(Page kind)
    {
        current = kind;
        if (page != null)
            Destroy(page.gameObject);
        page = MenuUI.Stretch(kind.ToString(), frame);
        return page;
    }

    // ───────────────────────────── title ─────────────────────────────

    private void ShowTitle()
    {
        RectTransform root = NewPage(Page.Title);
        // darken the drifting stage a little so the title and buttons read well
        MenuUI.Panel(MenuUI.Stretch("Shade", root), null, new Color(0.05f, 0.04f, 0.03f, 0.35f));

        string title = Catalog != null ? Catalog.gameTitle : "RUIN SURVIVORS";
        MenuUI.Label(root, title, 24f, 70f, 300f, 80f, 32, TextAnchor.UpperLeft, Theme?.accent, heading: true,
            shadow: true);
        MenuUI.Label(root, "Survive the endless ruins.", 26f, 150f, 260f, 12f, 8, color: Theme?.textDim, shadow: true);

        var items = new List<Selectable>();
        float x = 40f, y = 190f;
        MenuButton start = MenuUI.MakeButton(root, "START", x, y, 140f, 24f, ShowCharacters);
        items.Add(start);

        RunSaveData save = RunSaveSystem.Load();
        MenuButton resume = MenuUI.MakeButton(root, "RESUME", x, y + 30f, 140f, 24f, () =>
        {
            if (!RunSession.ResumeSavedRun())
                ShowTitle();
        });
        resume.interactable = save != null;
        items.Add(resume);
        if (save != null)
        {
            string stage = Catalog?.FindStage(save.stageScene)?.displayName ?? save.stageScene;
            string who = Catalog?.FindCharacter(save.characterId)?.displayName ?? save.characterId;
            RectTransform info = MenuUI.Box("Save Info", root, x + 148f, y + 31f, 200f, 22f);
            MenuUI.Panel(info, Theme?.panelDark);
            MenuUI.Label(info, $"{who} - {stage}\n{MenuUI.FormatTime(save.elapsedSeconds)}  Lv {save.level}   {save.savedAt}",
                5f, 2f, 192f, 20f, 8, color: Theme?.text);
        }

        items.Add(MenuUI.MakeButton(root, "OPTIONS", x, y + 60f, 140f, 24f, OpenOptions));
        items.Add(MenuUI.MakeButton(root, "QUIT", x, y + 90f, 140f, 24f, RunSession.QuitGame));
        MenuUI.LinkVertical(items.FindAll(s => s.interactable));
        MenuUI.Select(start);

        MenuUI.Label(root, "[ARROWS] Move   [ENTER] Select   [ESC] Back", 0f, 344f, 632f, 12f, 8,
            TextAnchor.UpperRight, Theme?.textDim, shadow: true);
    }

    private void OpenOptions()
    {
        Selectable back = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();
        options = OptionsPanel.Open(frame, () =>
        {
            options = null;
            MenuUI.Select(back);
        });
    }

    // ───────────────────────────── character select ─────────────────────────────

    private void ShowCharacters()
    {
        RectTransform root = NewPage(Page.Characters);
        MenuUI.Panel(MenuUI.Stretch("Shade", root), null, Theme != null ? Theme.backdrop : new Color(0, 0, 0, 0.7f));
        MenuUI.Label(root, "CHOOSE YOUR CHARACTER", 0f, 8f, 640f, 20f, 16, TextAnchor.UpperCenter, Theme?.text,
            heading: true, shadow: true);

        List<CharacterProfile> characters = Catalog != null ? Catalog.characters : new List<CharacterProfile>();
        characterIndex = Mathf.Clamp(chosenCharacter != null ? characters.IndexOf(chosenCharacter) : 0, 0,
            Mathf.Max(0, characters.Count - 1));

        // Portrait row
        RectTransform grid = MenuUI.Box("Portraits", root, 16f, 34f, 220f, 76f);
        MenuUI.Panel(grid, Theme?.panelDark);
        var cells = new List<Selectable>();
        for (int i = 0; i < characters.Count; i++)
        {
            int index = i;
            CharacterProfile character = characters[i];
            MenuButton cell = MenuUI.MakeButton(grid, "", 8f + i * 70f, 6f, 64f, 64f, () => ChooseCharacter(index));
            Image cellImage = cell.GetComponent<Image>();
            cellImage.sprite = Theme?.cell;
            cell.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = cell.colors;
            colors.normalColor = new Color(0.75f, 0.72f, 0.68f, 1f);
            colors.highlightedColor = colors.selectedColor = Color.white;
            colors.pressedColor = Theme != null ? Theme.highlight : Color.yellow;
            cell.colors = colors;
            MenuUI.Icon(cell.transform, character.portrait, 4f, 4f, 56f).preserveAspect = true;
            cell.ShowPointer = false;
            cell.Selected += () =>
            {
                MoveSelectionFrame(cell.transform as RectTransform);
                ShowCharacterInfo(index);
            };
            cells.Add(cell);
        }

        selectionFrame = MenuUI.Box("Selection Frame", grid, 0f, 0f, 70f, 70f);
        MenuUI.Panel(selectionFrame, Theme?.focus, Theme != null && Theme.focus != null ? Color.white : Theme?.highlight)
            .raycastTarget = false;

        for (int i = 0; i < cells.Count; i++)
        {
            Navigation nav = cells[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = cells[(i + cells.Count - 1) % cells.Count];
            nav.selectOnRight = cells[(i + 1) % cells.Count];
            cells[i].navigation = nav;
        }

        // Preview
        RectTransform previewBox = MenuUI.Window("Preview", root, 16f, 116f, 220f, 210f, out RectTransform preview);
        _ = previewBox;
        RectTransform spriteArea = MenuUI.Box("Sprite Area", preview, 50f, 0f, 108f, 108f);
        spriteArea.gameObject.AddComponent<RectMask2D>();
        var spriteObject = new GameObject("Sprite", typeof(RectTransform));
        RectTransform spriteRect = (RectTransform)spriteObject.transform;
        spriteRect.SetParent(spriteArea, false);
        spriteRect.anchorMin = spriteRect.anchorMax = spriteRect.pivot = new Vector2(0.5f, 0.5f);
        previewImage = spriteObject.AddComponent<Image>();
        previewImage.preserveAspect = false;
        previewImage.raycastTarget = false;
        previewAnimator = spriteRect.gameObject.AddComponent<MenuSpriteAnimator>();
        nameText = MenuUI.Label(preview, "", 0f, 108f, 208f, 18f, 16, TextAnchor.UpperCenter, Theme?.accent, true, true);
        epithetText = MenuUI.Label(preview, "", 0f, 128f, 208f, 12f, 8, TextAnchor.UpperCenter, Theme?.textDim);
        statsText = MenuUI.Label(preview, "", 16f, 146f, 176f, 40f, 8);

        // Info
        MenuUI.Window("Info", root, 248f, 34f, 376f, 292f, out infoContent);

        MenuUI.Label(root, "[ARROWS] Browse   [ENTER] Choose   [ESC] Back", 0f, 340f, 632f, 12f, 8,
            TextAnchor.UpperRight, Theme?.textDim, shadow: true);

        if (cells.Count > 0)
        {
            MenuUI.Select(cells[characterIndex]);
            ShowCharacterInfo(characterIndex);
        }
    }

    // Height of the character itself (not its sprite cell) in the preview box, the same for everyone.
    private const float PreviewCharacterHeight = 92f;
    private const float PreviewMaxWidth = 108f;

    /// <summary>
    /// Sizes the preview so the visible character is <see cref="PreviewCharacterHeight"/> tall and centred,
    /// however much empty space its sprite sheet cells have (Iris' cells are 128x80 around a 24x20 body).
    /// </summary>
    private void FitPreview(Sprite frame, Rect content)
    {
        if (frame == null || previewImage == null)
            return;

        Vector2 framePixels = frame.rect.size;
        if (content.width <= 0f || content.height <= 0f)
            content = new Rect(0f, 0f, 1f, 1f);

        float contentWidth = content.width * framePixels.x;
        float contentHeight = content.height * framePixels.y;
        float scale = Mathf.Min(PreviewCharacterHeight / contentHeight, PreviewMaxWidth / contentWidth);

        RectTransform rect = previewImage.rectTransform;
        rect.sizeDelta = framePixels * scale;

        // Move the image so the content centre lands on the box centre.
        Vector2 contentCenter = new Vector2(content.center.x * framePixels.x, content.center.y * framePixels.y);
        rect.anchoredPosition = (framePixels * 0.5f - contentCenter) * scale;
    }

    private void ShowCharacterInfo(int index)
    {
        List<CharacterProfile> characters = Catalog.characters;
        if (index < 0 || index >= characters.Count || infoContent == null)
            return;

        characterIndex = index;
        CharacterProfile c = characters[index];
        Sprite[] frames = c.idleFrames != null && c.idleFrames.Length > 0 ? c.idleFrames : new[] { c.portrait };
        previewAnimator.Play(frames, c.idleFramesPerSecond);
        FitPreview(frames[0], c.previewContent);
        nameText.text = c.displayName.ToUpperInvariant();
        epithetText.text = c.epithet;
        statsText.text = $"HP   {c.maxHealth}\nATK  {Stars(c.attackRating)}\nSPD  {Stars(c.speedRating)}";

        foreach (Transform child in infoContent)
            Destroy(child.gameObject);

        float w = infoContent.rect.width;
        float y = 0f;
        Color heading = Theme != null ? Theme.accent : Color.yellow;

        // Weapon
        y = Section(y, "WEAPON");
        MenuUI.Icon(infoContent, c.WeaponIcon, 0f, y, 24f);
        MenuUI.Label(infoContent, c.weaponTitle, 30f, y, w - 30f, 12f, 8, color: Theme?.highlight);
        Text weaponText = MenuUI.Label(infoContent, c.weaponDescription, 30f, y + 11f, w - 30f, 10f, 8);
        y += Mathf.Max(26f, 13f + weaponText.preferredHeight) + 4f;
        string forms = c.FormsSummary();
        if (!string.IsNullOrEmpty(forms))
        {
            MenuUI.Label(infoContent, $"Forms: {forms}", 30f, y - 2f, w - 30f, 10f, 8, color: Theme?.textDim);
            y += 10f;
        }

        // Ultimate
        y = Section(y, "ULTIMATE");
        string cooldown = c.formSwitch != null ? $"Switch CD {c.UltimateCooldown:0}s" : $"CD {c.UltimateCooldown:0}s";
        MenuUI.Label(infoContent, c.UltimateName, 0f, y, w - 90f, 12f, 8, color: Theme?.highlight);
        MenuUI.Label(infoContent, cooldown, w - 90f, y, 90f, 12f, 8, TextAnchor.UpperRight, Theme?.textDim);
        Text ultText = MenuUI.Label(infoContent, c.UltimateDescription, 0f, y + 11f, w, 10f, 8);
        y += 13f + ultText.preferredHeight + 4f;

        // Evolutions (the max-level forms of the main weapon)
        y = Section(y, "ULTIMATE EVOLUTIONS");
        foreach ((WeaponData weapon, WeaponEvolutionData evolution) in c.Evolutions())
        {
            MenuUI.Icon(infoContent, evolution.icon != null ? evolution.icon : weapon.icon, 0f, y, 16f);
            MenuUI.Label(infoContent, evolution.displayName, 22f, y, w - 22f, 12f, 8, color: Theme?.highlight);
            Text evo = MenuUI.Label(infoContent, evolution.description, 22f, y + 10f, w - 22f, 10f, 8, color: Theme?.text);
            y += 12f + evo.preferredHeight + 3f;
        }

        float Section(float top, string title)
        {
            MenuUI.Label(infoContent, title, 0f, top, w, 12f, 8, color: heading, heading: true);
            RectTransform line = MenuUI.Box("Line", infoContent, 0f, top + 10f, w, 1f);
            MenuUI.Panel(line, null, new Color(heading.r, heading.g, heading.b, 0.35f));
            return top + 14f;
        }
    }

    private void MoveSelectionFrame(RectTransform cell)
    {
        if (selectionFrame == null || cell == null)
            return;

        selectionFrame.anchoredPosition = cell.anchoredPosition + new Vector2(-3f, 3f);
        selectionFrame.SetAsLastSibling();
    }

    private static string Stars(int rating)
    {
        rating = Mathf.Clamp(rating, 0, 5);
        return new string('*', rating) + new string('.', 5 - rating);
    }

    private void ChooseCharacter(int index)
    {
        if (Catalog == null || index < 0 || index >= Catalog.characters.Count)
            return;

        chosenCharacter = Catalog.characters[index];
        ShowStages();
    }

    // ───────────────────────────── stage select ─────────────────────────────

    private void ShowStages()
    {
        RectTransform root = NewPage(Page.Stages);
        MenuUI.Panel(MenuUI.Stretch("Shade", root), null, Theme != null ? Theme.backdrop : new Color(0, 0, 0, 0.7f));
        MenuUI.Label(root, "CHOOSE A STAGE", 0f, 8f, 640f, 20f, 16, TextAnchor.UpperCenter, Theme?.text, true, true);
        MenuUI.Label(root, $"Character: {chosenCharacter.displayName}", 0f, 28f, 640f, 12f, 8, TextAnchor.UpperCenter,
            Theme?.textDim);

        List<StageProfile> stages = Catalog.stages;
        var cards = new List<Selectable>();
        float cardW = 280f, gap = 24f;
        float startX = (640f - (stages.Count * cardW + (stages.Count - 1) * gap)) / 2f;
        for (int i = 0; i < stages.Count; i++)
        {
            StageProfile stage = stages[i];
            float x = startX + i * (cardW + gap);
            MenuButton card = MenuUI.MakeButton(root, "", x, 50f, cardW, 270f, () =>
                RunSession.StartNewRun(chosenCharacter.characterId, stage.sceneName));
            card.GetComponent<Image>().sprite = Theme?.panel;
            card.ShowPointer = false;
            card.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = card.colors;
            colors.normalColor = new Color(0.7f, 0.68f, 0.64f, 1f);
            colors.highlightedColor = colors.selectedColor = Color.white;
            card.colors = colors;

            float picH = (cardW - 28f) * 152f / 272f; // previews are in-game captures at 272x152 aspect (1088x608)
            RectTransform pic = MenuUI.Box("Preview", card.transform, 10f, 10f, cardW - 20f, picH + 8f);
            MenuUI.Panel(pic, Theme?.panelDarker);
            Image image = MenuUI.Icon(pic, stage.preview, 4f, 4f, cardW - 28f);
            image.rectTransform.sizeDelta = new Vector2(cardW - 28f, picH);
            MenuUI.Label(card.transform, stage.displayName.ToUpperInvariant(), 12f, 178f, cardW - 24f, 18f, 16,
                TextAnchor.UpperCenter, Theme?.accent, true, true);
            MenuUI.Label(card.transform, stage.description, 16f, 200f, cardW - 32f, 60f, 8, TextAnchor.UpperCenter,
                Theme?.text, shadow: true);
            cards.Add(card);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            Navigation nav = cards[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = cards[(i + cards.Count - 1) % cards.Count];
            nav.selectOnRight = cards[(i + 1) % cards.Count];
            cards[i].navigation = nav;
        }

        MenuUI.Label(root, "[ARROWS] Browse   [ENTER] Start   [ESC] Back", 0f, 340f, 632f, 12f, 8,
            TextAnchor.UpperRight, Theme?.textDim, shadow: true);
        if (cards.Count > 0)
            MenuUI.Select(cards[Mathf.Clamp(stageIndex, 0, cards.Count - 1)]);
    }
}
