using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allocation-free versions of the Physics2D "All" queries (OverlapCircleAll, RaycastAll, ...).
///
/// Each call rents a list from a small pool and returns it wrapped in a disposable; always use it with
/// <c>using</c> so the list goes back to the pool:
/// <code>using PhysicsQuery.Results&lt;Collider2D&gt; hits = PhysicsQuery.OverlapCircle(center, radius, mask);
/// foreach (Collider2D c in hits.List) { ... }</code>
/// Lists are rented per call, so nested queries (a hit that triggers another query) are safe.
/// </summary>
public static class PhysicsQuery
{
    public readonly struct Results<T> : IDisposable
    {
        public readonly List<T> List;
        private readonly Stack<List<T>> owner;

        internal Results(List<T> list, Stack<List<T>> owner)
        {
            List = list;
            this.owner = owner;
        }

        public int Count => List.Count;
        public T this[int index] => List[index];

        public void Dispose()
        {
            if (List == null)
                return;
            List.Clear();
            owner.Push(List);
        }
    }

    private static readonly Stack<List<Collider2D>> ColliderLists = new();
    private static readonly Stack<List<RaycastHit2D>> HitLists = new();
    private static readonly Comparison<RaycastHit2D> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

    private static ContactFilter2D Filter(int layerMask)
    {
        var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
        filter.SetLayerMask(layerMask);
        return filter;
    }

    private static Results<Collider2D> RentColliders() =>
        new(ColliderLists.Count > 0 ? ColliderLists.Pop() : new List<Collider2D>(64), ColliderLists);

    private static Results<RaycastHit2D> RentHits() =>
        new(HitLists.Count > 0 ? HitLists.Pop() : new List<RaycastHit2D>(64), HitLists);

    public static Results<Collider2D> OverlapCircle(Vector2 center, float radius, int layerMask)
    {
        Results<Collider2D> results = RentColliders();
        Physics2D.OverlapCircle(center, radius, Filter(layerMask), results.List);
        return results;
    }

    public static Results<Collider2D> OverlapBox(Vector2 center, Vector2 size, float angle, int layerMask)
    {
        Results<Collider2D> results = RentColliders();
        Physics2D.OverlapBox(center, size, angle, Filter(layerMask), results.List);
        return results;
    }

    public static Results<Collider2D> OverlapArea(Vector2 pointA, Vector2 pointB, int layerMask)
    {
        Results<Collider2D> results = RentColliders();
        Physics2D.OverlapArea(pointA, pointB, Filter(layerMask), results.List);
        return results;
    }

    /// <summary>
    /// CircleCast (radius &gt; 0) or Raycast (radius = 0) returning every hit, sorted nearest first.
    /// </summary>
    public static Results<RaycastHit2D> CastAll(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask)
    {
        Results<RaycastHit2D> results = RentHits();
        ContactFilter2D filter = Filter(layerMask);

        if (radius > 0f)
            Physics2D.CircleCast(origin, radius, direction, filter, results.List, distance);
        else
            Physics2D.Raycast(origin, direction, filter, results.List, distance);

        results.List.Sort(ByDistance);
        return results;
    }
}
