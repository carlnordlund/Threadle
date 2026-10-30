using System;
using System.Collections.Generic;
using System.Text;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// Reusable graph traversal primitives and methods shared across centrality, path, and community
    /// analyses. All BFS variants are hyperedge-aware for 2-mode layers: each hyperedge is processed at
    /// most once (or once per BFS level in BFSBrandes), preventing O(N^2) work from large affiliations.
    /// Note that the point-to-point shortest path methods have their own BFS: this is because that one
    /// also has the option of recovering the actual path.
    /// </summary>
    internal static class GraphAlgorithms
    {
        #region Methods (internal)

        internal static void SplitLayers(IReadOnlyList<ILayer> layers, out List<ILayerOneMode> oneModes, out List<LayerTwoMode> twoModesDynamic, out List<LayerTwoModeStatic> twoModesStatic)
        {
            oneModes = [];
            twoModesDynamic = [];
            twoModesStatic = [];
            foreach (var layer in layers)
            {
                if (layer is LayerTwoModeStatic lts) twoModesStatic.Add(lts);
                else if (layer is LayerTwoMode ltm) twoModesDynamic.Add(ltm);
                else if (layer is ILayerOneMode lom) oneModes.Add(lom);
            }
        }

        /// <summary>
        /// Single-source BFS returning distances from <paramref name="source"/> to all reachable nodes.
        /// Hyperedge-aware: each 2-mode hyperedge is expanded at most once across the entire BFS.
        /// Uses <see cref="ILayerOneMode.VisitAlters"/> rather than <see cref="ILayerOneMode.GetNodeAlters"/>
        /// for the 1-mode traversal step, so no array/HashSet is allocated per node visited (see that
        /// method's remarks on why double-visiting a bidirectional neighbor under
        /// <see cref="EdgeTraversal.Both"/> is harmless here: <c>dist.TryAdd</c> is idempotent).
        /// </summary>
        internal static Dictionary<uint, int> BFS(uint source, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            var dist = new Dictionary<uint, int> { [source] = 0 };
            var queue = new Queue<uint>();
            queue.Enqueue(source);

            var dynVisited = Array.ConvertAll(twoModesDynamic.ToArray(),
                _ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance));
            var statVisited = Array.ConvertAll(twoModesStatic.ToArray(), _ => new HashSet<int>());

            int nextDist = 0;
            void VisitOneModeAlter(uint v)
            {
                if (dist.TryAdd(v, nextDist))
                    queue.Enqueue(v);
            }
            Action<uint> visitOneModeAlter = VisitOneModeAlter;

            while (queue.Count > 0)
            {
                uint u = queue.Dequeue();
                int d = dist[u];
                nextDist = d + 1;

                foreach (var layer in oneModes)
                    layer.VisitAlters(u, traversal, visitOneModeAlter);

                for (int li = 0; li < twoModesDynamic.Count; li++)
                {
                    var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(u);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                        if (dynVisited[li].Add(he))
                            foreach (uint m in he.NodeIds)
                                if (dist.TryAdd(m, d + 1))
                                    queue.Enqueue(m);
                }

                for (int li = 0; li < twoModesStatic.Count; li++)
                {
                    if (!twoModesStatic[li].TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                    for (int k = nStart; k < nEnd; k++)
                    {
                        int hIdx = twoModesStatic[li].GetNodeHyperedgeIndex(k);
                        if (!statVisited[li].Add(hIdx)) continue;
                        twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                        for (int j = hStart; j < hEnd; j++)
                        {
                            uint m = twoModesStatic[li].GetHyperedgeNodeAt(j);
                            if (dist.TryAdd(m, d + 1))
                                queue.Enqueue(m);
                        }
                    }
                }
            }
            return dist;
        }

        /// <summary>
        /// Single-source BFS returning all data needed for Brandes betweenness accumulation:
        /// distances, shortest-path counts (sigma), predecessor lists, and topological order.
        /// Hyperedge-aware: tracks the BFS level at which each hyperedge was first expanded.
        /// Same-level re-encounters update sigma/pred; deeper-level encounters are skipped.
        /// Uses <see cref="ILayerOneMode.VisitAlters"/> (no per-node array/HashSet allocation)
        /// for the 1-mode traversal step, except for <see cref="EdgeTraversal.Both"/> on a directed
        /// layer: there, visiting a bidirectional neighbor twice would double-count it in
        /// <paramref name="sigma"/>/<paramref name="pred"/> (unlike plain <see cref="BFS"/>, this
        /// method has per-visit side effects, not just an idempotent reachability mark), so that one
        /// combination falls back to <see cref="ILayerOneMode.GetNodeAlters"/>, which still dedupes.
        /// </summary>
        internal static void BFSBrandes(uint source, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal, out Dictionary<uint, int> dist, out Dictionary<uint, long> sigma, out Dictionary<uint, List<uint>> pred, out Stack<uint> order)
        {
            dist = new Dictionary<uint, int> { [source] = 0 };
            sigma = new Dictionary<uint, long> { [source] = 1L };
            pred = [];
            order = new Stack<uint>();
            var queue = new Queue<uint>();
            queue.Enqueue(source);

            var dynExpanded = Array.ConvertAll(twoModesDynamic.ToArray(),
                _ => new Dictionary<Hyperedge, int>(ReferenceEqualityComparer.Instance));
            var statExpanded = Array.ConvertAll(twoModesStatic.ToArray(), _ => new Dictionary<int, int>());

            uint currentU = source;
            int currentMemberDist = 0;
            void VisitOneModeAlter(uint v) => ProcessNeighbor(v, currentU, currentMemberDist, dist, sigma, pred, queue);
            Action<uint> visitOneModeAlter = VisitOneModeAlter;

            while (queue.Count > 0)
            {
                uint u = queue.Dequeue();
                order.Push(u);
                int memberDist = dist[u] + 1;
                currentU = u;
                currentMemberDist = memberDist;

                foreach (var layer in oneModes)
                {
                    // Both-on-directed needs GetNodeAlters' own dedup (see remarks above) - every
                    // other combination (Out, In, or any traversal on an undirected layer, where
                    // VisitAlters only ever takes its first branch) is safe without it.
                    if (traversal == EdgeTraversal.Both && layer.IsDirectional)
                        foreach (uint v in layer.GetNodeAlters(u, traversal))
                            ProcessNeighbor(v, u, memberDist, dist, sigma, pred, queue);
                    else
                        layer.VisitAlters(u, traversal, visitOneModeAlter);
                }

                for (int li = 0; li < twoModesDynamic.Count; li++)
                {
                    var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(u);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                    {
                        if (!dynExpanded[li].TryGetValue(he, out int expandedAt))
                        {
                            dynExpanded[li][he] = memberDist;
                            foreach (uint m in he.NodeIds)
                                if (m != u) ProcessNeighbor(m, u, memberDist, dist, sigma, pred, queue);
                        }
                        else if (expandedAt == memberDist)
                        {
                            foreach (uint m in he.NodeIds)
                                if (m != u && dist.TryGetValue(m, out int dm) && dm == memberDist)
                                {
                                    sigma[m] += sigma[u];
                                    pred[m].Add(u);
                                }
                        }
                    }
                }

                for (int li = 0; li < twoModesStatic.Count; li++)
                {
                    if (!twoModesStatic[li].TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                    for (int k = nStart; k < nEnd; k++)
                    {
                        int hIdx = twoModesStatic[li].GetNodeHyperedgeIndex(k);
                        if (!statExpanded[li].TryGetValue(hIdx, out int expandedAt))
                        {
                            statExpanded[li][hIdx] = memberDist;
                            twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                            for (int j = hStart; j < hEnd; j++)
                            {
                                uint m = twoModesStatic[li].GetHyperedgeNodeAt(j);
                                if (m != u) ProcessNeighbor(m, u, memberDist, dist, sigma, pred, queue);
                            }
                        }
                        else if (expandedAt == memberDist)
                        {
                            twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                            for (int j = hStart; j < hEnd; j++)
                            {
                                uint m = twoModesStatic[li].GetHyperedgeNodeAt(j);
                                if (m != u && dist.TryGetValue(m, out int dm) && dm == memberDist)
                                {
                                    sigma[m] += sigma[u];
                                    pred[m].Add(u);
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Methods (private)

        private static void ProcessNeighbor(uint v, uint u, int memberDist, Dictionary<uint, int> dist, Dictionary<uint, long> sigma,
            Dictionary<uint, List<uint>> pred, Queue<uint> queue)
        {
            if (!dist.ContainsKey(v))
            {
                dist[v] = memberDist;
                sigma[v] = sigma[u];
                pred[v] = [u];
                queue.Enqueue(v);
            }
            else if (dist[v] == memberDist)
            {
                sigma[v] += sigma[u];
                pred[v].Add(u);
            }
        }
        #endregion
    }
}
