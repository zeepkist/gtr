using System;
using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

internal static class HierarchyOwnership
{
    public static IReadOnlyList<T> ResolveRoots<T>(
        IEnumerable<T> anchors,
        T modelRoot,
        T excludedRoot,
        Func<T, T> getParent,
        IEqualityComparer<T> comparer = null)
        where T : class
    {
        comparer ??= EqualityComparer<T>.Default;
        if (anchors == null || modelRoot == null || getParent == null)
            return Array.Empty<T>();

        var validAnchors = new List<T>();
        foreach (T anchor in anchors)
        {
            if (anchor == null ||
                comparer.Equals(anchor, modelRoot) ||
                !IsSameOrDescendant(anchor, modelRoot, getParent, comparer) ||
                IsSameOrDescendant(anchor, excludedRoot, getParent, comparer) ||
                IsSameOrDescendant(excludedRoot, anchor, getParent, comparer) ||
                Contains(validAnchors, anchor, comparer))
            {
                continue;
            }

            validAnchors.Add(anchor);
        }

        if (validAnchors.Count == 0)
            return Array.Empty<T>();

        T commonRoot = FindNearestCommonAncestor(validAnchors, getParent, comparer);
        if (commonRoot != null &&
            !comparer.Equals(commonRoot, modelRoot) &&
            IsSameOrDescendant(commonRoot, modelRoot, getParent, comparer) &&
            !IsSameOrDescendant(commonRoot, excludedRoot, getParent, comparer) &&
            !IsSameOrDescendant(excludedRoot, commonRoot, getParent, comparer))
        {
            return new[] { commonRoot };
        }

        return GetTopLevelRoots(validAnchors, getParent, comparer);
    }

    public static T FindNearestCommonAncestor<T>(
        IReadOnlyList<T> nodes,
        Func<T, T> getParent,
        IEqualityComparer<T> comparer = null)
        where T : class
    {
        comparer ??= EqualityComparer<T>.Default;
        if (nodes == null || nodes.Count == 0 || getParent == null)
            return null;

        for (T candidate = nodes[0]; candidate != null; candidate = getParent(candidate))
        {
            bool containsAll = true;
            for (int i = 1; i < nodes.Count; i++)
            {
                if (IsSameOrDescendant(nodes[i], candidate, getParent, comparer))
                    continue;

                containsAll = false;
                break;
            }

            if (containsAll)
                return candidate;
        }

        return null;
    }

    public static bool IsSameOrDescendant<T>(
        T node,
        T root,
        Func<T, T> getParent,
        IEqualityComparer<T> comparer = null)
        where T : class
    {
        comparer ??= EqualityComparer<T>.Default;
        if (node == null || root == null || getParent == null)
            return false;

        for (T current = node; current != null; current = getParent(current))
        {
            if (comparer.Equals(current, root))
                return true;
        }

        return false;
    }

    public static IReadOnlyList<T> GetTopLevelRoots<T>(
        IEnumerable<T> nodes,
        Func<T, T> getParent,
        IEqualityComparer<T> comparer = null)
        where T : class
    {
        comparer ??= EqualityComparer<T>.Default;
        if (nodes == null || getParent == null)
            return Array.Empty<T>();

        var distinct = new List<T>();
        foreach (T node in nodes)
        {
            if (node != null && !Contains(distinct, node, comparer))
                distinct.Add(node);
        }

        var roots = new List<T>();
        foreach (T node in distinct)
        {
            bool hasCandidateAncestor = false;
            foreach (T candidate in distinct)
            {
                if (comparer.Equals(node, candidate) ||
                    !IsSameOrDescendant(node, candidate, getParent, comparer))
                {
                    continue;
                }

                hasCandidateAncestor = true;
                break;
            }

            if (!hasCandidateAncestor)
                roots.Add(node);
        }

        return roots;
    }

    private static bool Contains<T>(IEnumerable<T> nodes, T target, IEqualityComparer<T> comparer)
    {
        foreach (T node in nodes)
        {
            if (comparer.Equals(node, target))
                return true;
        }

        return false;
    }
}
