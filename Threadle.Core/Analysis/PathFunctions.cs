using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Analysis
{
    internal static class PathFunctions
    {
        /// <summary>
        /// Bidirectional BFS for a single source-target pair. Expands the smaller frontier
        /// each step and stops as soon as the combined distance cannot improve.
        /// Hyperedge-aware: per-direction visited sets ensure each 2-mode hyperedge is
        /// expanded at most once per direction.
        /// </summary>
        internal static ShortestPathResult BidirectionalBFS(uint source, uint target, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, bool returnPath)
        {
            var fwdDynVisited = Array.ConvertAll(twoModesDynamic.ToArray(), _ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance));
            var bwdDynVisited = Array.ConvertAll(twoModesDynamic.ToArray(), _ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance));
            var fwdStatVisited = Array.ConvertAll(twoModesStatic.ToArray(), _ => new HashSet<int>());
            var bwdStatVisited = Array.ConvertAll(twoModesStatic.ToArray(), _ => new HashSet<int>());

            var distFwd = new Dictionary<uint, int> { [source] = 0 };
            var distBwd = new Dictionary<uint, int> { [target] = 0 };
            Dictionary<uint, uint>? predFwd = returnPath ? [] : null;
            Dictionary<uint, uint>? predBwd = returnPath ? [] : null;
            List<uint> frontierFwd = [source];
            List<uint> frontierBwd = [target];
            int best = int.MaxValue;
            uint meetingNode = 0;
            int dFwd = 0, dBwd = 0;

            while (frontierFwd.Count > 0 || frontierBwd.Count > 0)
            {
                if (best != int.MaxValue && best <= dFwd + dBwd + 1)
                    break;

                if (frontierFwd.Count > 0 && (frontierBwd.Count == 0 || frontierFwd.Count <= frontierBwd.Count))
                {
                    dFwd++;
                    List<uint> next = [];
                    foreach (uint u in frontierFwd)
                    {
                        foreach (var layer in oneModes)
                            foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.Out))
                                if (distFwd.TryAdd(v, dFwd))
                                {
                                    next.Add(v);
                                    if (returnPath) predFwd![v] = u;
                                    if (distBwd.TryGetValue(v, out int bd))
                                    {
                                        int c = dFwd + bd;
                                        if (c < best) { best = c; meetingNode = v; }
                                    }
                                }

                        for (int li = 0; li < twoModesDynamic.Count; li++)
                        {
                            var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(u);
                            if (hec == null) continue;
                            foreach (var he in hec.HyperEdges)
                                if (fwdDynVisited[li].Add(he))
                                    foreach (uint m in he.NodeIds)
                                        if (distFwd.TryAdd(m, dFwd))
                                        {
                                            next.Add(m);
                                            if (returnPath) predFwd![m] = u;
                                            if (distBwd.TryGetValue(m, out int bd))
                                            {
                                                int c = dFwd + bd;
                                                if (c < best) { best = c; meetingNode = m; }
                                            }
                                        }
                        }

                        for (int li = 0; li < twoModesStatic.Count; li++)
                        {
                            if (!twoModesStatic[li].TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                            for (int k = nStart; k < nEnd; k++)
                            {
                                int hIdx = twoModesStatic[li].GetNodeHyperedgeIndex(k);
                                if (!fwdStatVisited[li].Add(hIdx)) continue;
                                twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                                for (int j = hStart; j < hEnd; j++)
                                {
                                    uint m = twoModesStatic[li].GetHyperedgeNodeAt(j);
                                    if (distFwd.TryAdd(m, dFwd))
                                    {
                                        next.Add(m);
                                        if (returnPath) predFwd![m] = u;
                                        if (distBwd.TryGetValue(m, out int bd))
                                        {
                                            int c = dFwd + bd;
                                            if (c < best) { best = c; meetingNode = m; }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    frontierFwd = next;
                }
                else
                {
                    dBwd++;
                    List<uint> next = [];
                    foreach (uint u in frontierBwd)
                    {
                        foreach (var layer in oneModes)
                            foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.In))
                                if (distBwd.TryAdd(v, dBwd))
                                {
                                    next.Add(v);
                                    if (returnPath) predBwd![v] = u;
                                    if (distFwd.TryGetValue(v, out int fd))
                                    {
                                        int c = fd + dBwd;
                                        if (c < best) { best = c; meetingNode = v; }
                                    }
                                }

                        for (int li = 0; li < twoModesDynamic.Count; li++)
                        {
                            var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(u);
                            if (hec == null) continue;
                            foreach (var he in hec.HyperEdges)
                                if (bwdDynVisited[li].Add(he))
                                    foreach (uint m in he.NodeIds)
                                        if (distBwd.TryAdd(m, dBwd))
                                        {
                                            next.Add(m);
                                            if (returnPath) predBwd![m] = u;
                                            if (distFwd.TryGetValue(m, out int fd))
                                            {
                                                int c = fd + dBwd;
                                                if (c < best) { best = c; meetingNode = m; }
                                            }
                                        }
                        }

                        for (int li = 0; li < twoModesStatic.Count; li++)
                        {
                            if (!twoModesStatic[li].TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                            for (int k = nStart; k < nEnd; k++)
                            {
                                int hIdx = twoModesStatic[li].GetNodeHyperedgeIndex(k);
                                if (!bwdStatVisited[li].Add(hIdx)) continue;
                                twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                                for (int j = hStart; j < hEnd; j++)
                                {
                                    uint m = twoModesStatic[li].GetHyperedgeNodeAt(j);
                                    if (distBwd.TryAdd(m, dBwd))
                                    {
                                        next.Add(m);
                                        if (returnPath) predBwd![m] = u;
                                        if (distFwd.TryGetValue(m, out int fd))
                                        {
                                            int c = fd + dBwd;
                                            if (c < best) { best = c; meetingNode = m; }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    frontierBwd = next;
                }
            }

            int distance = best == int.MaxValue ? -1 : best;

            uint[]? path = null;
            if (returnPath && distance >= 0)
            {
                var pathList = new List<uint>();
                uint cur = meetingNode;
                while (cur != source)
                {
                    pathList.Add(cur);
                    cur = predFwd![cur];
                }
                pathList.Add(source);
                pathList.Reverse();

                cur = meetingNode;
                while (cur != target)
                {
                    cur = predBwd![cur];
                    pathList.Add(cur);
                }
                path = [.. pathList];
            }

            return new ShortestPathResult(distance, path);
        }
    }
}