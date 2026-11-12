using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool for fire-and-forget visual effects (replaces Instantiate + Destroy(obj, lifetime)).
///
/// Only pure visuals are pooled (Transform, SpriteRenderer, Animator, ParticleSystem...). A prefab that
/// carries any MonoBehaviour may have its own life-cycle logic, so it falls back to Instantiate/Destroy.
/// Re-enabling restarts Animators (default state) and play-on-awake particle systems.
/// </summary>
public sealed class VfxPool : MonoBehaviour
{
    private struct Timed
    {
        public GameObject Instance;
        public GameObject Prefab;
        public float ReleaseTime;
    }

    private static VfxPool instance;

    private readonly Dictionary<GameObject, Stack<GameObject>> pools = new();
    private readonly Dictionary<GameObject, bool> poolable = new();
    private readonly List<Timed> active = new();

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime, float scaleMultiplier = 1f)
    {
        if (prefab == null)
            return null;

        VfxPool pool = GetOrCreate();
        if (!pool.IsPoolable(prefab))
        {
            GameObject created = Instantiate(prefab, position, rotation);
            created.transform.localScale = prefab.transform.localScale * scaleMultiplier;
            Destroy(created, lifetime);
            return created;
        }

        if (!pool.pools.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            pool.pools[prefab] = stack;
        }

        GameObject vfx = null;
        while (stack.Count > 0 && vfx == null)
            vfx = stack.Pop();

        if (vfx == null)
            vfx = Instantiate(prefab, pool.transform);

        Transform t = vfx.transform;
        t.SetPositionAndRotation(position, rotation);
        t.localScale = prefab.transform.localScale * scaleMultiplier;
        vfx.SetActive(true);

        pool.active.Add(new Timed { Instance = vfx, Prefab = prefab, ReleaseTime = Time.time + lifetime });
        return vfx;
    }

    private static VfxPool GetOrCreate()
    {
        if (instance != null)
            return instance;

        var host = new GameObject("[VfxPool]");
        instance = host.AddComponent<VfxPool>();
        return instance;
    }

    private bool IsPoolable(GameObject prefab)
    {
        if (!poolable.TryGetValue(prefab, out bool result))
        {
            result = prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0;
            poolable[prefab] = result;
        }

        return result;
    }

    private void Update()
    {
        float now = Time.time;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Timed timed = active[i];
            if (timed.ReleaseTime > now)
                continue;

            active.RemoveAt(i);
            if (timed.Instance == null)
                continue;

            timed.Instance.SetActive(false);
            pools[timed.Prefab].Push(timed.Instance);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
