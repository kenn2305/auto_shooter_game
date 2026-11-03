using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerExperience))]
public sealed class LevelUpDraftController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private LevelUpDraftConfig config;

    [Header("Player References")]
    [SerializeField] private PlayerExperience playerExperience;
    [SerializeField] private CharacterCombatSystem combatStats;
    [SerializeField] private Transform weaponRoot;
    [SerializeField] private Transform itemRoot;
    [SerializeField] private MonoBehaviour aimProviderBehaviour;

    [Header("Presentation")]
    [SerializeField] private LevelUpDraftUI draftUI;
    [SerializeField] private bool pauseGameWhileChoosing = true;

    [Header("Runtime Debug")]
    [SerializeField, Min(0)] private int pendingLevelUpCount;
    [SerializeField] private bool selectionActive;
    [SerializeField, Min(0)] private int totalSelectionsMade;

    private readonly Queue<int> pendingLevels = new();
    private readonly List<LevelUpChoice> currentChoices = new();
    private readonly List<CombatStatBonus> cumulativeStatPackageBonuses = new();
    private readonly Dictionary<string, int> statSelectionCounts = new();

    private System.Random random;
    private float timeScaleBeforeDraft = 1f;
    private bool ownsRuntimeConfig;
    private bool ownsRuntimeUI;

    public event Action<int, IReadOnlyList<LevelUpChoice>> DraftOpened;
    public event Action<LevelUpChoice> ChoiceApplied;
    public event Action DraftClosed;

    public bool SelectionActive => selectionActive;
    public int PendingLevelUpCount => pendingLevels.Count;
    public int TotalSelectionsMade => totalSelectionsMade;
    public IReadOnlyList<LevelUpChoice> CurrentChoices => currentChoices;
    public LevelUpDraftConfig Config => config;

    private void Awake()
    {
        ResolveReferences();
        ResolveConfig();
        random = new System.Random(unchecked(Environment.TickCount * 397 ^ GetInstanceID()));
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (playerExperience != null)
        {
            playerExperience.LevelGained -= HandlePlayerLevelGained;
            playerExperience.LevelGained += HandlePlayerLevelGained;
        }

        if (cumulativeStatPackageBonuses.Count > 0 && combatStats != null)
        {
            combatStats.SetStatBonuses(this, CombatStatSource.Passive, cumulativeStatPackageBonuses);
        }
    }

    private void OnDisable()
    {
        if (playerExperience != null)
        {
            playerExperience.LevelGained -= HandlePlayerLevelGained;
        }

        combatStats?.RemoveStatProvider(this);
        if (selectionActive)
        {
            CloseDraftSession();
        }
    }

    private void OnDestroy()
    {
        if (ownsRuntimeUI && draftUI != null)
        {
            Destroy(draftUI.gameObject);
        }

        if (ownsRuntimeConfig && config != null)
        {
            Destroy(config);
        }
    }

    public IReadOnlyList<LevelUpChoice> GenerateChoices(int playerLevel)
    {
        ResolveConfig();
        if (random == null)
        {
            random = new System.Random(unchecked(Environment.TickCount * 397 ^ GetInstanceID()));
        }

        return GenerateChoices(playerLevel, random);
    }

    public IReadOnlyList<LevelUpChoice> GenerateChoices(int playerLevel, int seed)
    {
        ResolveConfig();
        return GenerateChoices(playerLevel, new System.Random(seed));
    }

    public bool ApplyChoice(LevelUpChoice choice)
    {
        if (choice == null)
        {
            return false;
        }

        bool applied = choice.Kind switch
        {
            LevelUpChoiceKind.NewWeapon => AcquireWeapon(choice.WeaponDefinition),
            LevelUpChoiceKind.WeaponUpgrade => choice.OwnedWeapon != null && choice.OwnedWeapon.LevelUp(),
            LevelUpChoiceKind.NewItem => AcquireItem(choice.ItemDefinition),
            LevelUpChoiceKind.ItemUpgrade => choice.OwnedItem != null && choice.OwnedItem.LevelUp(),
            LevelUpChoiceKind.StatPackage => ApplyStatPackage(choice.StatDefinition),
            _ => false,
        };

        if (applied)
        {
            totalSelectionsMade++;
            ChoiceApplied?.Invoke(choice);
        }

        return applied;
    }

    public bool SelectCurrentChoice(int choiceIndex)
    {
        if (!selectionActive || pendingLevels.Count == 0 || choiceIndex < 0 || choiceIndex >= currentChoices.Count)
        {
            return false;
        }

        LevelUpChoice choice = currentChoices[choiceIndex];
        if (!ApplyChoice(choice))
        {
            Debug.LogWarning(
                $"Upgrade '{choice.Title}' became invalid before selection; regenerating draft.",
                this
            );
            ShowNextDraft();
            return false;
        }

        pendingLevels.Dequeue();
        UpdatePendingDebugCount();
        ShowNextDraft();
        return true;
    }

    [ContextMenu("Debug/Open Draft For Current Level")]
    public void DebugOpenDraft()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int level = playerExperience != null ? playerExperience.CurrentLevel : 2;
        pendingLevels.Enqueue(Mathf.Max(2, level));
        UpdatePendingDebugCount();
        if (!selectionActive)
        {
            ShowNextDraft();
        }
#endif
    }

    private void HandlePlayerLevelGained(int newLevel)
    {
        pendingLevels.Enqueue(newLevel);
        UpdatePendingDebugCount();
        if (!selectionActive)
        {
            ShowNextDraft();
        }
    }

    private void ShowNextDraft()
    {
        if (pendingLevels.Count == 0)
        {
            CloseDraftSession();
            return;
        }

        if (!selectionActive)
        {
            selectionActive = true;
            if (pauseGameWhileChoosing)
            {
                timeScaleBeforeDraft = Time.timeScale;
                Time.timeScale = 0f;
            }
        }

        int draftLevel = pendingLevels.Peek();
        currentChoices.Clear();
        currentChoices.AddRange(GenerateChoices(draftLevel));
        if (currentChoices.Count == 0)
        {
            Debug.LogError("Level Up draft has no valid choices. Check LevelUpDraftConfig.", this);
            pendingLevels.Dequeue();
            UpdatePendingDebugCount();
            ShowNextDraft();
            return;
        }

        ResolveUI();
        draftUI.Show(draftLevel, currentChoices, HandleUISelection);
        DraftOpened?.Invoke(draftLevel, currentChoices);
    }

    private void HandleUISelection(int choiceIndex)
    {
        if (!SelectCurrentChoice(choiceIndex) && selectionActive && pendingLevels.Count > 0)
        {
            draftUI.Show(pendingLevels.Peek(), currentChoices, HandleUISelection);
        }
    }

    private void CloseDraftSession()
    {
        if (!selectionActive)
        {
            return;
        }

        selectionActive = false;
        currentChoices.Clear();
        draftUI?.Hide();
        if (pauseGameWhileChoosing)
        {
            Time.timeScale = timeScaleBeforeDraft;
        }

        DraftClosed?.Invoke();
    }

    private List<LevelUpChoice> GenerateChoices(int playerLevel, System.Random rng)
    {
        List<WeightedCandidate> newEquipment = new();
        List<WeightedCandidate> ownedUpgrades = new();
        List<WeightedCandidate> statPackages = new();
        BuildEquipmentCandidates(playerLevel, newEquipment, ownedUpgrades);
        BuildStatCandidates(playerLevel, statPackages);

        LevelUpDraftPhase phase = config.GetPhase(playerLevel);
        int desiredCount = Mathf.Max(3, config.choiceCount);
        List<LevelUpChoice> result = new(desiredCount);
        HashSet<string> selectedKeys = new(StringComparer.Ordinal);

        if (phase.guaranteeNewEquipmentWhenSlotAvailable)
        {
            AddWeightedCandidate(result, selectedKeys, newEquipment, rng);
        }

        if (phase.guaranteeOwnedUpgradeWhenAvailable && result.Count < desiredCount)
        {
            AddWeightedCandidate(result, selectedKeys, ownedUpgrades, rng);
        }

        while (result.Count < desiredCount)
        {
            List<WeightedCandidate> pool = PickCategoryPool(newEquipment, ownedUpgrades, statPackages, phase, rng);
            if (pool == null || !AddWeightedCandidate(result, selectedKeys, pool, rng))
            {
                break;
            }
        }

        if (result.Count < desiredCount)
        {
            AddEmergencyStatChoices(result, selectedKeys, desiredCount, rng);
        }

        return result;
    }

    private void BuildEquipmentCandidates(
        int playerLevel,
        List<WeightedCandidate> newEquipment,
        List<WeightedCandidate> ownedUpgrades
    )
    {
        WeaponController[] ownedWeapons = GetComponentsInChildren<WeaponController>(true);
        LevelUpItemController[] ownedItems = GetComponentsInChildren<LevelUpItemController>(true);
        int weaponCount = CountConfiguredWeapons(ownedWeapons);
        int itemCount = CountConfiguredItems(ownedItems);

        if (config.weapons != null)
        {
            for (int i = 0; i < config.weapons.Count; i++)
            {
                LevelUpWeaponOfferDefinition definition = config.weapons[i];
                if (
                    definition?.ResolvedData == null
                    || string.IsNullOrWhiteSpace(definition.StableId) || playerLevel < definition.minimumPlayerLevel
                )
                {
                    continue;
                }

                WeaponController owned = FindOwnedWeapon(ownedWeapons, definition.ResolvedData);
                if (owned != null)
                {
                    if (!owned.IsMaxLevel)
                    {
                        ownedUpgrades.Add(
                            new WeightedCandidate(
                                LevelUpChoice.UpgradeWeapon(definition, owned),
                                definition.offerWeight
                            )
                        );
                    }

                    continue;
                }

                if (weaponCount < config.maximumWeaponSlots)
                {
                    newEquipment.Add(
                        new WeightedCandidate(LevelUpChoice.NewWeapon(definition), definition.offerWeight)
                    );
                }
            }
        }

        if (config.items == null)
        {
            return;
        }

        for (int i = 0; i < config.items.Count; i++)
        {
            LevelUpItemOfferDefinition definition = config.items[i];
            if (
                definition?.ResolvedData == null
                || string.IsNullOrWhiteSpace(definition.StableId) || playerLevel < definition.minimumPlayerLevel
            )
            {
                continue;
            }

            LevelUpItemController owned = FindOwnedItem(ownedItems, definition.ResolvedData);
            if (owned != null)
            {
                if (!owned.IsMaxLevel)
                {
                    ownedUpgrades.Add(
                        new WeightedCandidate(LevelUpChoice.UpgradeItem(definition, owned), definition.offerWeight)
                    );
                }

                continue;
            }

            if (itemCount < config.maximumItemSlots)
            {
                newEquipment.Add(new WeightedCandidate(LevelUpChoice.NewItem(definition), definition.offerWeight));
            }
        }
    }

    private void BuildStatCandidates(int playerLevel, List<WeightedCandidate> result)
    {
        if (config.statPackages == null)
        {
            return;
        }

        for (int i = 0; i < config.statPackages.Count; i++)
        {
            LevelUpStatDefinition definition = config.statPackages[i];
            if (definition == null || !definition.IsConfigured || playerLevel < definition.minimumPlayerLevel)
            {
                continue;
            }

            int selected = GetStatSelectionCount(definition.statID);
            if (definition.maximumSelections > 0 && selected >= definition.maximumSelections)
            {
                continue;
            }

            result.Add(new WeightedCandidate(LevelUpChoice.Stat(definition), definition.offerWeight));
        }
    }

    private void AddEmergencyStatChoices(
        List<LevelUpChoice> result,
        HashSet<string> selectedKeys,
        int desiredCount,
        System.Random rng
    )
    {
        List<WeightedCandidate> fallback = new();
        IReadOnlyList<LevelUpStatDefinition> definitions = CreateEmergencyStats();
        for (int i = 0; i < definitions.Count; i++)
        {
            LevelUpChoice choice = LevelUpChoice.Stat(definitions[i]);
            if (!selectedKeys.Contains(choice.Key))
            {
                fallback.Add(new WeightedCandidate(choice, definitions[i].offerWeight));
            }
        }

        while (result.Count < desiredCount && fallback.Count > 0)
        {
            AddWeightedCandidate(result, selectedKeys, fallback, rng);
        }
    }

    private static List<WeightedCandidate> PickCategoryPool(
        List<WeightedCandidate> newEquipment,
        List<WeightedCandidate> ownedUpgrades,
        List<WeightedCandidate> statPackages,
        LevelUpDraftPhase phase,
        System.Random rng
    )
    {
        float newWeight = newEquipment.Count > 0 ? Mathf.Max(0f, phase.newEquipmentWeight) : 0f;
        float ownedWeight = ownedUpgrades.Count > 0 ? Mathf.Max(0f, phase.ownedUpgradeWeight) : 0f;
        float statWeight = statPackages.Count > 0 ? Mathf.Max(0f, phase.statPackageWeight) : 0f;
        float total = newWeight + ownedWeight + statWeight;
        if (total <= 0f)
        {
            if (ownedUpgrades.Count > 0)
            {
                return ownedUpgrades;
            }

            if (newEquipment.Count > 0)
            {
                return newEquipment;
            }

            return statPackages.Count > 0 ? statPackages : null;
        }

        double roll = rng.NextDouble() * total;
        if (roll < newWeight)
        {
            return newEquipment;
        }

        roll -= newWeight;
        return roll < ownedWeight ? ownedUpgrades : statPackages;
    }

    private static bool AddWeightedCandidate(
        List<LevelUpChoice> result,
        HashSet<string> selectedKeys,
        List<WeightedCandidate> pool,
        System.Random rng
    )
    {
        while (pool.Count > 0)
        {
            int index = PickWeightedIndex(pool, rng);
            WeightedCandidate candidate = pool[index];
            pool.RemoveAt(index);
            if (candidate.Choice != null && selectedKeys.Add(candidate.Choice.Key))
            {
                result.Add(candidate.Choice);
                return true;
            }
        }

        return false;
    }

    private static int PickWeightedIndex(List<WeightedCandidate> pool, System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            total += Mathf.Max(0f, pool[i].Weight);
        }

        if (total <= 0f)
        {
            return rng.Next(0, pool.Count);
        }

        double roll = rng.NextDouble() * total;
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= Mathf.Max(0f, pool[i].Weight);
            if (roll <= 0d)
            {
                return i;
            }
        }

        return pool.Count - 1;
    }

    private bool AcquireWeapon(LevelUpWeaponOfferDefinition definition)
    {
        WeaponData data = definition?.ResolvedData;
        if (data == null)
        {
            return false;
        }

        WeaponController[] owned = GetComponentsInChildren<WeaponController>(true);
        if (FindOwnedWeapon(owned, data) != null || CountConfiguredWeapons(owned) >= config.maximumWeaponSlots)
        {
            return false;
        }

        Transform parent = weaponRoot != null ? weaponRoot : transform;
        WeaponController controller;
        if (definition.acquisitionPrefab != null)
        {
            controller = Instantiate(definition.acquisitionPrefab, parent, false);
        }
        else
        {
            GameObject weaponObject = new($"{data.displayName} Weapon");
            weaponObject.SetActive(false);
            weaponObject.transform.SetParent(parent, false);
            controller = weaponObject.AddComponent<WeaponController>();
        }

        bool configured = controller.ConfigureRuntime(data, combatStats, controller.transform, aimProviderBehaviour);
        controller.gameObject.SetActive(configured);
        if (!configured)
        {
            Destroy(controller.gameObject);
        }

        return configured;
    }

    private bool AcquireItem(LevelUpItemOfferDefinition definition)
    {
        LevelUpItemData data = definition?.ResolvedData;
        if (data == null)
        {
            return false;
        }

        LevelUpItemController[] owned = GetComponentsInChildren<LevelUpItemController>(true);
        if (FindOwnedItem(owned, data) != null || CountConfiguredItems(owned) >= config.maximumItemSlots)
        {
            return false;
        }

        Transform parent = itemRoot != null ? itemRoot : transform;
        LevelUpItemController controller;
        if (definition.acquisitionPrefab != null)
        {
            controller = Instantiate(definition.acquisitionPrefab, parent, false);
        }
        else
        {
            GameObject itemObject = new($"{data.displayName} Item");
            itemObject.SetActive(false);
            itemObject.transform.SetParent(parent, false);
            controller = itemObject.AddComponent<LevelUpItemController>();
        }

        bool configured = controller.ConfigureRuntime(data, combatStats);
        controller.gameObject.SetActive(configured);
        if (!configured)
        {
            Destroy(controller.gameObject);
        }

        return configured;
    }

    private bool ApplyStatPackage(LevelUpStatDefinition definition)
    {
        if (definition == null || !definition.IsConfigured || combatStats == null)
        {
            return false;
        }

        int selected = GetStatSelectionCount(definition.statID);
        if (definition.maximumSelections > 0 && selected >= definition.maximumSelections)
        {
            return false;
        }

        statSelectionCounts[definition.statID] = selected + 1;
        cumulativeStatPackageBonuses.AddRange(definition.bonuses);
        combatStats.SetStatBonuses(this, CombatStatSource.Passive, cumulativeStatPackageBonuses);
        return true;
    }

    private int GetStatSelectionCount(string statID)
    {
        return !string.IsNullOrWhiteSpace(statID) && statSelectionCounts.TryGetValue(statID, out int count) ? count : 0;
    }

    private void ResolveReferences()
    {
        if (playerExperience == null)
        {
            playerExperience = GetComponent<PlayerExperience>();
        }

        if (combatStats == null)
        {
            combatStats = GetComponentInChildren<CharacterCombatSystem>(true);
        }

        if (weaponRoot == null && combatStats != null)
        {
            weaponRoot = combatStats.transform;
        }

        if (itemRoot == null)
        {
            itemRoot = transform;
        }

        if (aimProviderBehaviour == null)
        {
            MonoBehaviour[] candidates = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IWeaponAimProvider2D)
                {
                    aimProviderBehaviour = candidates[i];
                    break;
                }
            }
        }
    }

    private void ResolveConfig()
    {
        if (config == null)
        {
            config = Resources.Load<LevelUpDraftConfig>(LevelUpDraftConfig.ResourceName);
        }

        if (config != null)
        {
            return;
        }

        config = ScriptableObject.CreateInstance<LevelUpDraftConfig>();
        config.statPackages = new List<LevelUpStatDefinition>(CreateEmergencyStats());
        config.phases = new List<LevelUpDraftPhase> { LevelUpDraftPhase.CreateFallback() };
        ownsRuntimeConfig = true;
        Debug.LogWarning(
            "LevelUpDraftConfig was not found in Resources; using safe runtime Stat packages.",
            this
        );
    }

    private void ResolveUI()
    {
        if (draftUI != null)
        {
            return;
        }

        draftUI = FindFirstObjectByType<LevelUpDraftUI>(FindObjectsInactive.Include);
        if (draftUI == null)
        {
            draftUI = LevelUpDraftUI.CreateRuntime();
            ownsRuntimeUI = true;
        }
    }

    private void UpdatePendingDebugCount()
    {
        pendingLevelUpCount = pendingLevels.Count;
    }

    private static int CountConfiguredWeapons(WeaponController[] controllers)
    {
        int count = 0;
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i].Data != null)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountConfiguredItems(LevelUpItemController[] controllers)
    {
        int count = 0;
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i].Data != null)
            {
                count++;
            }
        }

        return count;
    }

    private static WeaponController FindOwnedWeapon(WeaponController[] controllers, WeaponData data)
    {
        string id = GetWeaponId(data);
        for (int i = 0; i < controllers.Length; i++)
        {
            WeaponData ownedData = controllers[i]?.Data;
            if (ownedData == data || (!string.IsNullOrWhiteSpace(id) && GetWeaponId(ownedData) == id))
            {
                return controllers[i];
            }
        }

        return null;
    }

    private static LevelUpItemController FindOwnedItem(LevelUpItemController[] controllers, LevelUpItemData data)
    {
        string id = GetItemId(data);
        for (int i = 0; i < controllers.Length; i++)
        {
            LevelUpItemData ownedData = controllers[i]?.Data;
            if (ownedData == data || (!string.IsNullOrWhiteSpace(id) && GetItemId(ownedData) == id))
            {
                return controllers[i];
            }
        }

        return null;
    }

    private static string GetWeaponId(WeaponData data)
    {
        return data == null ? string.Empty : !string.IsNullOrWhiteSpace(data.weaponID) ? data.weaponID : data.name;
    }

    private static string GetItemId(LevelUpItemData data)
    {
        return data == null ? string.Empty : !string.IsNullOrWhiteSpace(data.itemID) ? data.itemID : data.name;
    }

    private static IReadOnlyList<LevelUpStatDefinition> CreateEmergencyStats()
    {
        return new[]
        {
            LevelUpStatDefinition.CreateFallback(
                "atk_up",
                "ATK Up",
                "+8% Attack Power",
                CombatStatType.AttackPowerPercent,
                8f
            ),
            LevelUpStatDefinition.CreateFallback(
                "speed_up",
                "Speed Up",
                "+12% Movement Speed",
                CombatStatType.MovementSpeedPercent,
                12f
            ),
            LevelUpStatDefinition.CreateFallback(
                "crit_up",
                "Critical Up",
                "+3% Critical Chance",
                CombatStatType.CriticalChancePercent,
                3f
            ),
            LevelUpStatDefinition.CreateFallback(
                "pickup_up",
                "Pickup Range Up",
                "+20% EXP Pickup Range",
                CombatStatType.ExperiencePickupRangePercent,
                20f
            ),
        };
    }

    private sealed class WeightedCandidate
    {
        public LevelUpChoice Choice { get; }
        public float Weight { get; }

        public WeightedCandidate(LevelUpChoice choice, float weight)
        {
            Choice = choice;
            Weight = weight;
        }
    }
}
