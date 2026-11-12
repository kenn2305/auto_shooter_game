using UnityEngine;

/// <summary>Title screen camera: drifts slowly over the endless stage (the map streams around it).</summary>
public sealed class MenuCameraDrift : MonoBehaviour
{
    [SerializeField] private Vector2 velocity = new(1.2f, 0.45f);
    [SerializeField, Min(0f)] private float sway = 0.6f;
    [SerializeField, Min(0.01f)] private float swaySpeed = 0.15f;

    private Vector3 start;

    private void Start()
    {
        start = transform.position;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        Vector2 offset = velocity * t + new Vector2(0f, Mathf.Sin(t * swaySpeed * Mathf.PI * 2f) * sway);
        transform.position = new Vector3(start.x + offset.x, start.y + offset.y, start.z);
    }
}
