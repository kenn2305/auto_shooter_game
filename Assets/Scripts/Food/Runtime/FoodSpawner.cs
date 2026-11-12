using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drops healing food at random free spots inside the camera view.
/// Every <see cref="rollInterval"/> seconds of gameplay it rolls <see cref="spawnChancePercent"/>; on success
/// a random food appears (never on an obstacle, never right next to the player). Touching it heals
/// <see cref="healPercent"/>% of the player's max HP. Uses scaled time, so nothing spawns while paused.
/// Built by Tools > Gameplay > Setup Food Spawner, which also assigns the sprites from Assets/Food.
/// </summary>
[DisallowMultipleComponent]
public sealed class FoodSpawner : MonoBehaviour
{
    [Header("Foods")]
    [SerializeField] private Sprite[] foods = System.Array.Empty<Sprite>();

    [Header("Spawning")]
    [SerializeField, Min(0.1f)] private float rollInterval = 5f;
    [SerializeField, Range(0f, 100f)] private float spawnChancePercent = 15f;
    [SerializeField, Min(1)] private int maxActive = 3;
    [Tooltip("Fraction of the view kept clear at each edge, so food never appears half off-screen.")]
    [SerializeField, Range(0f, 0.4f)] private float viewMargin = 0.1f;
    [SerializeField, Min(0f)] private float minDistanceFromPlayer = 3f;
    [Tooltip("Spots overlapping these layers (trees, ruins...) are rejected.")]
    [SerializeField] private LayerMask blockedLayers;
    [SerializeField, Min(0.05f)] private float clearanceRadius = 0.7f;

    [Header("Pickup")]
    [SerializeField, Range(1f, 100f)] private float healPercent = 20f;
    [Tooltip("Distance from the player's body centre at which food is eaten.")]
    [SerializeField, Min(0.1f)] private float pickupRadius = 1.3f;
    [Tooltip("World height of a food sprite (the player's body is ~2.4).")]
    [SerializeField, Min(0.1f)] private float worldHeight = 1.3f;
    [SerializeField] private int sortingOrder = -54; // ground layer, just above EXP gems (-55)

    private readonly List<FoodPickup> active = new();
    private readonly Stack<FoodPickup> pool = new();
    private float rollTimer;
    private PlayerHealth player;

    public int ActiveCount => active.Count;

    private void Awake()
    {
        if (blockedLayers.value == 0)
        {
            blockedLayers = LayerMask.GetMask("Ostacle");
        }
    }

    private void Update()
    {
        if (player == null || !player.isActiveAndEnabled)
        {
            player = FindFirstObjectByType<PlayerHealth>();
            if (player == null)
            {
                return;
            }
        }

        rollTimer += Time.deltaTime;
        while (rollTimer >= rollInterval)
        {
            rollTimer -= rollInterval;
            if (active.Count < maxActive && foods.Length > 0 && Random.value * 100f < spawnChancePercent)
            {
                TrySpawn();
            }
        }

        CollectNearby();
    }

    /// <summary>Spawns one food right away (debug / scripted drops). Returns false when no free spot was found.</summary>
    public bool TrySpawn()
    {
        Camera view = Camera.main;
        if (view == null || foods.Length == 0)
        {
            return false;
        }

        float halfHeight = view.orthographicSize * (1f - 2f * viewMargin);
        float halfWidth = view.orthographicSize * view.aspect * (1f - 2f * viewMargin);
        Vector2 center = view.transform.position;
        Vector2 playerPosition = player != null ? (Vector2)player.transform.position : center;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 spot = center + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfHeight, halfHeight));
            if ((spot - playerPosition).sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer)
            {
                continue;
            }

            if (Physics2D.OverlapCircle(spot, clearanceRadius, blockedLayers) != null)
            {
                continue;
            }

            FoodPickup food = pool.Count > 0 ? pool.Pop() : CreatePickup();
            food.Show(foods[Random.Range(0, foods.Length)], spot, worldHeight);
            active.Add(food);
            return true;
        }

        return false;
    }

    private void CollectNearby()
    {
        if (player == null || !player.IsAlive)
        {
            return;
        }

        Collider2D body = player.GetComponent<Collider2D>();
        Vector2 playerPosition = body != null ? (Vector2)body.bounds.center : (Vector2)player.transform.position;
        float radiusSquared = pickupRadius * pickupRadius;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            FoodPickup food = active[i];
            if (((Vector2)food.transform.position - playerPosition).sqrMagnitude > radiusSquared)
            {
                continue;
            }

            int heal = Mathf.CeilToInt(player.MaxHealth * healPercent / 100f);
            player.Heal(heal);
            food.Hide();
            active.RemoveAt(i);
            pool.Push(food);
        }
    }

    private FoodPickup CreatePickup()
    {
        GameObject go = new("Food");
        go.transform.SetParent(transform, false);
        FoodPickup food = go.AddComponent<FoodPickup>();
        food.Initialize(sortingOrder);
        return food;
    }
}
