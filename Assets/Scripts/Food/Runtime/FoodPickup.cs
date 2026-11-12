using UnityEngine;

/// <summary>
/// One pooled food on the ground: pops in when it appears, then bobs gently. Collected by FoodSpawner.
/// </summary>
[DisallowMultipleComponent]
public sealed class FoodPickup : MonoBehaviour
{
    private const float PopDuration = 0.25f;
    private const float BobHeight = 0.12f;
    private const float BobSpeed = 3f;

    private Transform visual;
    private SpriteRenderer spriteRenderer;
    private float baseScale = 1f;
    private float age;
    private float phase;

    public void Initialize(int sortingOrder)
    {
        visual = new GameObject("Sprite").transform;
        visual.SetParent(transform, false);
        spriteRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = sortingOrder;
        gameObject.SetActive(false);
    }

    public void Show(Sprite sprite, Vector2 position, float worldHeight)
    {
        spriteRenderer.sprite = sprite;
        float spriteHeight = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.y) : 1f;
        baseScale = worldHeight / spriteHeight;
        transform.position = new Vector3(position.x, position.y, 0f);
        age = 0f;
        phase = Random.value * Mathf.PI * 2f;
        gameObject.SetActive(true);
        Animate();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        age += Time.deltaTime; // scaled: freezes while gameplay is paused
        Animate();
    }

    private void Animate()
    {
        // Overshooting pop on spawn, then a slow bob.
        float pop = age < PopDuration ? Mathf.Sin(age / PopDuration * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f) : 1f;
        visual.localScale = Vector3.one * (baseScale * Mathf.Max(0.05f, pop));
        visual.localPosition = new Vector3(0f, Mathf.Sin(age * BobSpeed + phase) * BobHeight, 0f);
    }
}
