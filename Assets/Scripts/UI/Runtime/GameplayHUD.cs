using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-run HUD: EXP bar + level, survival timer, kill counter, character portrait whose border
/// fills up with the Ultimate cooldown, the main weapon slot, weapon slots and item slots.
/// Built by Tools > UI > Build Gameplay HUD; it finds the active character on its own, so it works
/// for every character (Iris and Lily use CharacterUltimateController, Zena uses CharacterFormSwitchController).
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayHUD : MonoBehaviour
{
    [Serializable]
    public sealed class CharacterPortrait
    {
        [Tooltip("Matched against the character's GameObject name (case-insensitive, 'Zena(Clone)' matches 'Zena').")]
        public string characterName;
        public Sprite portrait;
    }

    [Header("Characters")]
    [SerializeField] private List<CharacterPortrait> portraits = new();
    [SerializeField] private Sprite fallbackPortrait;

    [Header("Experience")]
    [SerializeField] private Image experienceFill;
    [SerializeField] private Text levelText;
    [SerializeField, Min(0.1f)] private float experienceFillSpeed = 2.5f;

    [Header("Timer & Kills")]
    [SerializeField] private Text timerText;
    [SerializeField] private Text killText;
    [SerializeField] private RectTransform killPunchTarget;

    [Header("Portrait & Ultimate")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private RectTransform portraitRoot;
    [SerializeField] private Image ultimateRing;
    [SerializeField] private Text ultimateCooldownText;
    [SerializeField] private Color ringChargingColor = new(0.36f, 0.62f, 0.95f, 1f);
    [SerializeField] private Color ringReadyColor = new(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private Color ringReadyPulseColor = new(1f, 0.97f, 0.75f, 1f);
    [SerializeField] private Color ringActiveColor = new(1f, 0.38f, 0.62f, 1f);

    [Header("Slots")]
    [SerializeField] private HUDSlotView mainWeaponSlot;
    [SerializeField] private HUDSlotView[] weaponSlots = Array.Empty<HUDSlotView>();
    [SerializeField] private HUDSlotView[] itemSlots = Array.Empty<HUDSlotView>();
    [SerializeField, Min(0.05f)] private float slotRefreshInterval = 0.2f;

    // Bound character.
    private PlayerExperience experience;
    private CharacterUltimateController ultimate;
    private CharacterFormSwitchController formSwitch;
    private WeaponController mainWeapon;

    private readonly List<WeaponController> weaponBuffer = new();
    private readonly List<LevelUpItemController> itemBuffer = new();

    private float elapsedTime;
    private int shownSeconds = -1;
    private int killCount;
    private int shownKills = -1;
    private int shownLevel = -1;
    private float shownExperience;
    private int shownCooldownSeconds = int.MinValue;
    private float cooldownTotal;
    private float lastRemainingCooldown;
    private bool wasReady;
    private float portraitPunch;
    private float killPunch;
    private float nextSlotRefresh;
    private float nextBindAttempt;

    public int KillCount => killCount;
    public float ElapsedTime => elapsedTime;

    private void OnEnable()
    {
        EnemyHealth.AnyDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        EnemyHealth.AnyDied -= HandleEnemyDied;
    }

    private void Start()
    {
        elapsedTime = 0f;
        killCount = 0;
        TryBindCharacter();
        RefreshAll();
    }

    /// <summary>Continues the timer and kill counter of a resumed run.</summary>
    public void RestoreProgress(float elapsedSeconds, int kills)
    {
        elapsedTime = Mathf.Max(0f, elapsedSeconds);
        killCount = Mathf.Max(0, kills);
        shownKills = -1;
        UpdateTimer();
        UpdateKills();
    }

    private void Update()
    {
        if ((experience == null || !experience.isActiveAndEnabled) && Time.unscaledTime >= nextBindAttempt)
        {
            nextBindAttempt = Time.unscaledTime + 0.5f;
            TryBindCharacter();
        }

        elapsedTime += Time.deltaTime;
        UpdateTimer();
        UpdateKills();
        UpdateExperience();
        UpdateUltimate();

        // Unscaled: weapons and items are picked while the level-up draft has paused the game.
        if (Time.unscaledTime >= nextSlotRefresh)
        {
            nextSlotRefresh = Time.unscaledTime + slotRefreshInterval;
            RefreshSlots();
        }

        UpdatePunches();
    }

    /// <summary>Points the HUD at another character (e.g. after a character select).</summary>
    public void Bind(PlayerExperience character)
    {
        experience = character;
        ultimate = null;
        formSwitch = null;
        mainWeapon = null;
        shownLevel = -1;
        shownExperience = 0f;
        wasReady = false;

        if (character == null)
        {
            return;
        }

        ultimate = character.GetComponentInChildren<CharacterUltimateController>(true);
        formSwitch = character.GetComponentInChildren<CharacterFormSwitchController>(true);
        mainWeapon = formSwitch != null ? formSwitch.MainWeapon : null;
        if (mainWeapon == null)
        {
            // The weapon the character starts with is its main weapon; everything picked later is a level-up weapon.
            CollectWeapons(character, weaponBuffer);
            mainWeapon = weaponBuffer.Count > 0 ? weaponBuffer[0] : null;
        }

        cooldownTotal = ResolveInitialCooldownTotal();
        lastRemainingCooldown = GetRemainingCooldown();

        if (portraitImage != null)
        {
            portraitImage.sprite = ResolvePortrait(character.gameObject.name);
            portraitImage.enabled = portraitImage.sprite != null;
        }

        RefreshAll();
    }

    private void TryBindCharacter()
    {
        PlayerExperience[] candidates = FindObjectsByType<PlayerExperience>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].isActiveAndEnabled)
            {
                if (candidates[i] != experience)
                {
                    Bind(candidates[i]);
                }

                return;
            }
        }
    }

    private void RefreshAll()
    {
        shownSeconds = -1;
        shownKills = -1;
        shownCooldownSeconds = int.MinValue;
        UpdateTimer();
        UpdateKills();
        UpdateExperience();
        UpdateUltimate();
        RefreshSlots();
    }

    // ---------------------------------------------------------------- Timer / kills

    private void UpdateTimer()
    {
        int seconds = Mathf.FloorToInt(elapsedTime);
        if (seconds == shownSeconds || timerText == null)
        {
            return;
        }

        shownSeconds = seconds;
        timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        killCount++;
        killPunch = 1f;
    }

    private void UpdateKills()
    {
        if (killCount == shownKills || killText == null)
        {
            return;
        }

        shownKills = killCount;
        killText.text = killCount.ToString();
    }

    // ---------------------------------------------------------------- Experience

    private void UpdateExperience()
    {
        if (experience == null)
        {
            return;
        }

        int level = experience.CurrentLevel;
        float target = Mathf.Clamp01(experience.NormalizedProgress);
        if (level != shownLevel)
        {
            if (shownLevel > 0 && level > shownLevel)
            {
                // Leveled up: restart the bar from empty so the new progress fills in visibly.
                shownExperience = 0f;
            }

            shownLevel = level;
            if (levelText != null)
            {
                levelText.text = $"LV {level}";
            }
        }

        shownExperience = target < shownExperience
            ? target
            : Mathf.MoveTowards(shownExperience, target, experienceFillSpeed * Time.unscaledDeltaTime);
        if (experienceFill != null)
        {
            experienceFill.fillAmount = shownExperience;
        }
    }

    // ---------------------------------------------------------------- Ultimate border

    private float ResolveInitialCooldownTotal()
    {
        float remaining = GetRemainingCooldown();
        if (remaining > 0f)
        {
            return remaining;
        }

        if (ultimate != null && ultimate.Data != null)
        {
            return ultimate.Data.cooldown;
        }

        return formSwitch != null && formSwitch.Data != null ? formSwitch.Data.switchCooldown : 0f;
    }

    private float GetRemainingCooldown()
    {
        if (ultimate != null && ultimate.Data != null)
        {
            return ultimate.RemainingCooldown;
        }

        return formSwitch != null ? formSwitch.RemainingSwitchCooldown : 0f;
    }

    private void UpdateUltimate()
    {
        if (ultimateRing == null)
        {
            return;
        }

        bool hasSkill = (ultimate != null && ultimate.Data != null) || (formSwitch != null && formSwitch.Data != null);
        if (!hasSkill)
        {
            ultimateRing.fillAmount = 0f;
            SetCooldownText(-1);
            return;
        }

        // Active skill (Iris / Lily with a duration): the border drains while the skill lasts.
        if (ultimate != null && ultimate.IsActive)
        {
            float duration = ultimate.Data.duration;
            ultimateRing.fillAmount = duration > 0f ? Mathf.Clamp01(ultimate.RemainingDuration / duration) : 1f;
            ultimateRing.color = ringActiveColor;
            SetCooldownText(-1);
            wasReady = false;
            lastRemainingCooldown = 0f;
            return;
        }

        float remaining = GetRemainingCooldown();
        if (remaining > lastRemainingCooldown + 0.01f)
        {
            // A new cooldown started: its starting value is the full length of the border.
            cooldownTotal = remaining;
        }

        lastRemainingCooldown = remaining;

        if (remaining > 0f)
        {
            float total = Mathf.Max(cooldownTotal, remaining, 0.01f);
            ultimateRing.fillAmount = 1f - remaining / total;
            ultimateRing.color = ringChargingColor;
            SetCooldownText(Mathf.CeilToInt(remaining));
            wasReady = false;
            return;
        }

        if (!wasReady)
        {
            wasReady = true;
            portraitPunch = 1f;
        }

        ultimateRing.fillAmount = 1f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        ultimateRing.color = Color.Lerp(ringReadyColor, ringReadyPulseColor, pulse);
        SetCooldownText(-1);
    }

    private void SetCooldownText(int seconds)
    {
        if (ultimateCooldownText == null || seconds == shownCooldownSeconds)
        {
            return;
        }

        shownCooldownSeconds = seconds;
        ultimateCooldownText.enabled = seconds > 0;
        ultimateCooldownText.text = seconds > 0 ? seconds.ToString() : string.Empty;
    }

    // ---------------------------------------------------------------- Slots

    private void RefreshSlots()
    {
        if (experience == null)
        {
            mainWeaponSlot?.SetEmpty();
            ClearSlots(weaponSlots, 0);
            ClearSlots(itemSlots, 0);
            return;
        }

        if (mainWeaponSlot != null)
        {
            if (mainWeapon != null && mainWeapon.Data != null)
            {
                ShowWeapon(mainWeaponSlot, mainWeapon);
            }
            else
            {
                mainWeaponSlot.SetEmpty();
            }
        }

        CollectWeapons(experience, weaponBuffer);
        int weaponIndex = 0;
        for (int i = 0; i < weaponBuffer.Count && weaponIndex < weaponSlots.Length; i++)
        {
            if (weaponBuffer[i] == mainWeapon)
            {
                continue;
            }

            ShowWeapon(weaponSlots[weaponIndex], weaponBuffer[i]);
            weaponIndex++;
        }

        ClearSlots(weaponSlots, weaponIndex);

        itemBuffer.Clear();
        experience.GetComponentsInChildren(true, itemBuffer);
        int itemIndex = 0;
        for (int i = 0; i < itemBuffer.Count && itemIndex < itemSlots.Length; i++)
        {
            LevelUpItemController item = itemBuffer[i];
            if (item == null || item.Data == null || itemSlots[itemIndex] == null)
            {
                continue;
            }

            itemSlots[itemIndex].SetContent(item, item.Data.icon, item.DisplayLevel, item.IsMaxLevel && item.MaximumLevel > 1);
            itemIndex++;
        }

        ClearSlots(itemSlots, itemIndex);
    }

    private static void ShowWeapon(HUDSlotView slot, WeaponController weapon)
    {
        if (slot == null)
        {
            return;
        }

        // Evolved weapons show their evolution icon; otherwise the current form's icon (Zena swaps forms).
        Sprite icon = weapon.ActiveEvolution != null ? weapon.ActiveEvolution.icon : null;
        if (icon == null && weapon.Data != null)
        {
            icon = weapon.Data.icon;
        }

        if (icon == null && weapon.OwnershipData != null)
        {
            icon = weapon.OwnershipData.icon;
        }

        slot.SetContent(weapon, icon, weapon.DisplayLevel, weapon.IsMaxLevel && weapon.MaximumLevel > 1);
    }

    private static void ClearSlots(HUDSlotView[] slots, int fromIndex)
    {
        for (int i = fromIndex; i < slots.Length; i++)
        {
            slots[i]?.SetEmpty();
        }
    }

    private static void CollectWeapons(Component owner, List<WeaponController> result)
    {
        result.Clear();
        owner.GetComponentsInChildren(true, result);
        result.RemoveAll(weapon => weapon == null || weapon.IsSkillWeapon || weapon.Data == null);
    }

    // ---------------------------------------------------------------- Misc

    private Sprite ResolvePortrait(string characterName)
    {
        for (int i = 0; i < portraits.Count; i++)
        {
            CharacterPortrait entry = portraits[i];
            if (
                entry != null && entry.portrait != null && !string.IsNullOrWhiteSpace(entry.characterName)
                && characterName.IndexOf(entry.characterName, StringComparison.OrdinalIgnoreCase) >= 0
            )
            {
                return entry.portrait;
            }
        }

        return fallbackPortrait;
    }

    private void UpdatePunches()
    {
        portraitPunch = Mathf.MoveTowards(portraitPunch, 0f, Time.unscaledDeltaTime * 4f);
        killPunch = Mathf.MoveTowards(killPunch, 0f, Time.unscaledDeltaTime * 6f);

        if (portraitRoot != null)
        {
            portraitRoot.localScale = Vector3.one * (1f + 0.12f * portraitPunch * portraitPunch);
        }

        if (killPunchTarget != null)
        {
            killPunchTarget.localScale = Vector3.one * (1f + 0.15f * killPunch * killPunch);
        }
    }
}
