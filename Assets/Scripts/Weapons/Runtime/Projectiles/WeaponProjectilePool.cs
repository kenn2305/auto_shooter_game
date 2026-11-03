using System.Collections.Generic;
using UnityEngine;

public static class WeaponProjectilePool
{
    private static readonly Dictionary<WeaponProjectileActor, Stack<WeaponProjectileActor>> Pools =
        new();

    private static Transform poolRoot;

    public static WeaponProjectileActor Rent(WeaponProjectileActor prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        if (!Pools.TryGetValue(prefab, out Stack<WeaponProjectileActor> pool))
        {
            pool = new Stack<WeaponProjectileActor>();
            Pools.Add(prefab, pool);
        }

        WeaponProjectileActor instance = null;
        while (pool.Count > 0 && instance == null)
        {
            instance = pool.Pop();
        }

        if (instance == null)
        {
            instance = Object.Instantiate(prefab);
        }

        instance.SetPoolSource(prefab);
        instance.transform.SetParent(null, true);
        instance.gameObject.SetActive(true);
        return instance;
    }

    public static void Return(WeaponProjectileActor instance, WeaponProjectileActor sourcePrefab)
    {
        if (instance == null)
        {
            return;
        }

        if (sourcePrefab == null)
        {
            Object.Destroy(instance.gameObject);
            return;
        }

        if (!Pools.TryGetValue(sourcePrefab, out Stack<WeaponProjectileActor> pool))
        {
            pool = new Stack<WeaponProjectileActor>();
            Pools.Add(sourcePrefab, pool);
        }

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), false);
        pool.Push(instance);
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null)
        {
            return poolRoot;
        }

        GameObject root = new GameObject("[Weapon Projectile Pool]");
        root.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(root);
        poolRoot = root.transform;
        return poolRoot;
    }
}
