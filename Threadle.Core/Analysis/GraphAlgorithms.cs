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
        /// <summary>
        /// Reusable per-thread scratch state for <see cref="BFSBrandes"/>, so repeated calls across
        /// many source nodes (one per <see cref="System.Threading.Tasks.Parallel.For"/> iteration -
        /// see CentralityFunctions.AccumulateBFSCentralitiesForSources/
        /// AccumulateEdgeBetweennessForSources) reset existing collections instead of allocating a
        /// fresh Dictionary/Stack/Queue on every single call. External benchmarking found
        /// betweenness scaled poorly across threads; this per-source allocation churn - happening
        /// inside the parallel loop body, competing threads for the GC - was the confirmed cause.
        /// One instance is created per thread (via Parallel.For's local-state factory) and reused
        /// for every source that thread ends up handling.
        /// </summary>
        internal sealed class BFSBrandesScratch
        {
            internal readonly Dictionary<uint, int> Dist = [];
            internal readonly Dictionary<uint, long> Sigma = [];
            internal readonly Dictionary<uint, List<uint>> Pred = [];
            internal readonly Stack<uint> Order = [];
            internal readonly Queue<uint> Queue = [];

            private Dictionary<Hyperedge, int>[] _dynExpanded = [];
            private Dictionary<int, int>[] _statExpanded = [];

            internal Dictionary<Hyperedge, int>[] DynExpanded => _dynExpanded;
            internal Dictionary<int, int>[] StatExpanded => _statExpanded;

            /// <summary>Resets all state for a new source node, growing the per-2-mode-layer
            /// expansion-tracking arrays only the first time (or if the layer count changed) -
            /// otherwise their existing Dictionary instances are cleared, not reallocated.</summary>
            internal void Reset(uint source, int dynCount, int statCount)
            {
                Dist.Clear(); Dist[source] = 0;
                Sigma.Clear(); Sigma[source] = 1L;
                Pred.Clear();
                Order.Clear();
                Queue.Clear(); Queue.Enqueue(source);

                if (_dynExpanded.Length != dynCount)
                {
                    _dynExpanded = new Dictionary<Hyperedge, int>[dynCount];
                    for (int i = 0; i < dynCount; i++)
                        _dynExpanded[i] = new Dictionary<Hyperedge, int>(ReferenceEqualityComparer.Instance);
                }
                else
                    foreach (var d in _dynExpanded) d.Clear();

                if (_statExpanded.Length != statCount)
                {
                    _statExpanded = new Dictionary<int, int>[statCount];
                    for (int i = 0; i < statCount; i++)
                        _statExpanded[i] = new Dictionary<int, int>();
                }
                else
                    foreach (var d in _statExpanded) d.Clear();
            }
        }

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
        /// Takes a <paramref name="scratch"/> buffer (one per thread, reused across that thread's
        /// sources - see <see cref="BFSBrandesScratch"/>) rather than allocating and returning its
        /// own dist/sigma/pred/order each call; the caller reads them off <paramref name="scratch"/>
        /// after this returns.
        /// </summary>
        internal static void BFSBrandes(uint source, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal, BFSBrandesScratch scratch)
        {
            scratch.Reset(source, twoModesDynamic.Count, twoModesStatic.Count);
            var dist = scratch.Dist;
            var sigma = scratch.Sigma;
            var pred = scratch.Pred;
            var order = scratch.Order;
            var queue = scratch.Queue;
            var dynExpanded = scratch.DynExpanded;
            var statExpanded = scratch.StatExpanded;

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
