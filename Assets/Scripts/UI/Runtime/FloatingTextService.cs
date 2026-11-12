using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pop-up numbers: green "+N" when the player heals, red N when an enemy is hit, and bigger
/// yellow-orange N with a thick black outline on critical hits.
///
/// Numbers are pooled UI Texts on a pixel-snapped overlay canvas (sharp pixel font) that follow the world
/// point they were spawned at. They use scaled time, so they freeze while gameplay is paused.
/// The scene object (Tools > UI > Build Gameplay HUD) carries the font; one is created if it is missing.
/// </summary>
[DisallowMultipleComponent]
public sealed class FloatingTextService : MonoBehaviour
{
    private enum Style
    {
        Damage,
        Critical,
        Heal,
    }

    private sealed class Entry
    {
        public RectTransform Rect;
        public Text Text;
        public Outline Outline;
        public Vector3 WorldPosition;
        public float Age;
        public float Lifetime;
        public float PopScale;
        public bool Active;
    }

    [SerializeField] private Font font;

    [Header("Colours")]
    [SerializeField] private Color damageColor = new(1f, 0.28f, 0.26f, 1f);
    [SerializeField] private Color criticalColor = new(1f, 0.72f, 0.12f, 1f);
    [SerializeField] private Color healColor = new(0.36f, 1f, 0.42f, 1f);
    [SerializeField] private Color outlineColor = new(0f, 0f, 0f, 1f);

    [Header("Sizes (canvas units; multiples of 16 stay sharp)")]
    [SerializeField] private int damageSize = 32;
    [SerializeField] private int criticalSize = 48;
    [SerializeField] private int healSize = 32;

    [Header("Motion")]
    [SerializeField, Min(0.1f)] private float lifetime = 0.7f;
    [SerializeField, Min(0.1f)] private float criticalLifetime = 0.9f;
    [Tooltip("World units the number rises over its lifetime.")]
    [SerializeField] private float riseDistance = 0.9f;
    [Tooltip("Random horizontal spread so numbers on the same target do not stack exactly.")]
    [SerializeField] private float horizontalJitter = 0.35f;
    [SerializeField, Min(8)] private int poolSize = 96;

    private static FloatingTextService instance;

    private readonly List<Entry> entries = new();
    private RectTransform canvasRect;
    private Camera worldCamera;
    private int nextReuse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null && FindFirstObjectByType<FloatingTextService>() == null)
        {
            new GameObject("Floating Text", typeof(RectTransform)).AddComponent<FloatingTextService>();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildCanvas();
    }

    private void OnEnable()
    {
        EnemyHealth.AnyDamaged += HandleEnemyDamaged;
        PlayerHealth.AnyHealed += HandlePlayerHealed;
    }

    private void OnDisable()
    {
        EnemyHealth.AnyDamaged -= HandleEnemyDamaged;
        PlayerHealth.AnyHealed -= HandlePlayerHealed;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void HandleEnemyDamaged(EnemyHealth enemy, int amount, bool critical)
    {
        if (amount > 0)
        {
            Spawn(enemy.TopPosition, amount.ToString(), critical ? Style.Critical : Style.Damage);
        }
    }

    private void HandlePlayerHealed(PlayerHealth player, int amount)
    {
        PlayerHealthBar bar = player.GetComponent<PlayerHealthBar>();
        Vector3 position = bar != null ? bar.AboveBarPosition : player.transform.position + Vector3.up * 1.8f;
        Spawn(position, $"+{amount}", Style.Heal);
    }

    private void Spawn(Vector3 worldPosition, string value, Style style)
    {
        Entry entry = TakeEntry();
        entry.WorldPosition = worldPosition + new Vector3(Random.Range(-horizontalJitter, horizontalJitter), 0f, 0f);
        entry.Age = 0f;
        entry.Active = true;
        entry.Text.text = value;

        switch (style)
        {
            case Style.Critical:
                entry.Text.fontSize = criticalSize;
                entry.Text.color = criticalColor;
                entry.Outline.effectDistance = new Vector2(4f, -4f); // thick black outline
                entry.Lifetime = criticalLifetime;
                entry.PopScale = 1.5f;
                break;
            case Style.Heal:
                entry.Text.fontSize = healSize;
                entry.Text.color = healColor;
                entry.Outline.effectDistance = new Vector2(2f, -2f);
                entry.Lifetime = lifetime;
                entry.PopScale = 1.2f;
                break;
            default:
                entry.Text.fontSize = damageSize;
                entry.Text.color = damageColor;
                entry.Outline.effectDistance = new Vector2(2f, -2f);
                entry.Lifetime = lifetime;
                entry.PopScale = 1.2f;
                break;
        }

        entry.Rect.SetAsLastSibling(); // newest on top
        entry.Rect.gameObject.SetActive(true);
        Place(entry);
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime; // scaled: numbers freeze with the game
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (!entry.Active)
            {
                continue;
            }

            entry.Age += deltaTime;
            if (entry.Age >= entry.Lifetime)
            {
                entry.Active = false;
                entry.Rect.gameObject.SetActive(false);
                continue;
            }

            Place(entry);
        }
    }

    private void Place(Entry entry)
    {
        if (worldCamera == null || !worldCamera.isActiveAndEnabled)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }
        }

        float t = entry.Age / entry.Lifetime;
        float rise = 1f - (1f - t) * (1f - t); // ease-out
        Vector3 world = entry.WorldPosition + Vector3.up * (riseDistance * rise);
        Vector3 screen = worldCamera.WorldToScreenPoint(world);
        if (screen.z < 0f)
        {
            entry.Rect.gameObject.SetActive(false);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        entry.Rect.anchoredPosition = local;

        // Quick pop at spawn, then normal size; fade out over the last 30%.
        float pop = t < 0.12f ? Mathf.Lerp(entry.PopScale, 1f, t / 0.12f) : 1f;
        entry.Rect.localScale = new Vector3(pop, pop, 1f);
        float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
        Color color = entry.Text.color;
        color.a = alpha;
        entry.Text.color = color;
        Color outline = outlineColor;
        outline.a = alpha;
        entry.Outline.effectColor = outline;
    }

    private Entry TakeEntry()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].Active)
            {
                return entries[i];
            }
        }

        // Pool full: recycle the oldest-started slot in turn.
        Entry reused = entries[nextReuse];
        nextReuse = (nextReuse + 1) % entries.Count;
        return reused;
    }

    private void BuildCanvas()
    {
        Font usedFont = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // under the HUD (100) and the level-up draft (1000)
        canvas.pixelPerfect = true;
        if (GetComponent<CanvasScaler>() == null)
        {
            gameObject.AddComponent<CanvasScaler>();
        }

        canvasRect = (RectTransform)transform;

        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = new($"Number {i}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(240f, 64f);

            Text text = go.AddComponent<Text>();
            text.font = usedFont;
            text.alignment = TextAnchor.LowerCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = outlineColor;

            go.SetActive(false);
            entries.Add(new Entry { Rect = rect, Text = text, Outline = outline });
        }

        // Same pixel scale as the gameplay HUD (half size at 1080p); added after the Texts so their font is registered.
        PixelPerfectCanvasScale pixelScale = gameObject.AddComponent<PixelPerfectCanvasScale>();
        pixelScale.Configure(1890f, 0.5f, 0.5f);
    }
}
