using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Array-backed result of <see cref="BFS"/>: a drop-in replacement for the
        /// <c>Dictionary&lt;uint,int&gt;</c> it used to return, exposing the same read shapes
        /// (indexer/<see cref="TryGetValue"/>, <see cref="Keys"/>, <see cref="Values"/>, and
        /// pair-enumeration via <see cref="GetEnumerator"/>) every caller already used - only the
        /// backing storage changes, from a hashed dictionary built fresh per call to a plain
        /// <c>int[]</c> indexed via a <paramref name="nodeIndex"/> built once per top-level
        /// operation (see <see cref="BFS"/>'s remarks). <c>-1</c> in <paramref name="dist"/> means
        /// "not reached", matching the old dictionary's "key absent" for an unreached node.
        /// </summary>
        internal readonly struct BFSDistances(int[] dist, Dictionary<uint, int> nodeIndex, uint[] nodeIds)
        {
            public int this[uint nodeId] => nodeIndex.TryGetValue(nodeId, out int i) && dist[i] >= 0 ? dist[i] : -1;

            public bool TryGetValue(uint nodeId, out int d)
            {
                d = this[nodeId];
                return d >= 0;
            }

            public IEnumerable<(uint nodeId, int dist)> Reached
            {
                get
                {
                    for (int i = 0; i < dist.Length; i++)
                        if (dist[i] >= 0)
                            yield return (nodeIds[i], dist[i]);
                }
            }

            public IEnumerable<uint> Keys => Reached.Select(r => r.nodeId);
            public IEnumerable<int> Values => Reached.Select(r => r.dist);

            public IEnumerator<(uint nodeId, int dist)> GetEnumerator() => Reached.GetEnumerator();
        }

        /// <summary>
        /// Reusable scratch state for <see cref="BFSReachedNodes"/>, shared across the many
        /// sequential BFS calls one component-discovery pass makes (<see cref="Functions.ConnectedComponents"/>,
        /// <see cref="NetworkLevelFunctions.LargestComponent"/> - both loop over unvisited seed
        /// nodes, not <see cref="System.Threading.Tasks.Parallel.For"/>, so a single shared instance
        /// is safe here, unlike <see cref="BFSBrandesScratch"/>, which needs one per thread). Each
        /// call bumps a generation counter instead of clearing an n-sized array, so a graph
        /// fragmented into many small components doesn't pay an O(n) reset for every single seed
        /// node regardless of how small that seed's own component turns out to be.
        /// </summary>
        internal sealed class BFSReachScratch
        {
            private int[] _stamp = [];
            private int _currentStamp;

            internal int[] Stamp => _stamp;
            internal int CurrentStamp => _currentStamp;

            /// <summary>Starts a new call: grows the stamp array only the first time (or if the
            /// node universe size changed), otherwise reuses it - bumping the generation counter
            /// instead of clearing it.</summary>
            internal void StartCall(int n)
            {
                if (_stamp.Length != n)
                    _stamp = new int[n];
                _currentStamp++;
            }
        }

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
        /// <see cref="EdgeTraversal.Both"/> is harmless here: array writes below are idempotent once
        /// a slot is set).
        ///
        /// <paramref name="nodeIndex"/>/<paramref name="nodeIds"/> are the compact 0..n-1 numbering
        /// for this call's node universe (every caller's whole analysis, e.g. the full network or a
        /// single largest-component array - NOT rebuilt per source): built once by the caller and
        /// reused across every BFS call in that operation, so repeated calls (one per node, as
        /// eccentricity()/diameter()/closeness() all do) don't each pay to grow a fresh dictionary
        /// from nothing. Visited/distance state during traversal is a plain <c>int[]</c> indexed by
        /// that numbering instead of a <c>Dictionary&lt;uint,int&gt;</c> - no per-entry overhead, no
        /// incremental resize as the frontier grows. Translating a newly-discovered neighbor id back
        /// to its array slot still costs one dictionary lookup (via <paramref name="nodeIndex"/>) -
        /// unavoidable since <see cref="ILayerOneMode.VisitAlters"/> hands back raw node ids, not
        /// this numbering - but that dictionary itself is built once, not once per call.
        /// </summary>
        internal static BFSDistances BFS(uint source, Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            int[] dist = new int[nodeIds.Length];
            Array.Fill(dist, -1);
            int sourceIdx = nodeIndex[source];
            dist[sourceIdx] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(sourceIdx);

            var dynVisited = Array.ConvertAll(twoModesDynamic.ToArray(),
                _ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance));
            var statVisited = Array.ConvertAll(twoModesStatic.ToArray(), _ => new HashSet<int>());

            int nextDist = 0;
            void VisitOneModeAlter(uint v)
            {
                if (nodeIndex.TryGetValue(v, out int vIdx) && dist[vIdx] < 0)
                {
                    dist[vIdx] = nextDist;
                    queue.Enqueue(vIdx);
                }
            }
            Action<uint> visitOneModeAlter = VisitOneModeAlter;

            while (queue.Count > 0)
            {
                int uIdx = queue.Dequeue();
                uint u = nodeIds[uIdx];
                int d = dist[uIdx];
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
                                if (nodeIndex.TryGetValue(m, out int mIdx) && dist[mIdx] < 0)
                                {
                                    dist[mIdx] = d + 1;
                                    queue.Enqueue(mIdx);
                                }
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
                            if (nodeIndex.TryGetValue(m, out int mIdx) && dist[mIdx] < 0)
                            {
                                dist[mIdx] = d + 1;
                                queue.Enqueue(mIdx);
                            }
                        }
                    }
                }
            }
            return new BFSDistances(dist, nodeIndex, nodeIds);
        }

        /// <summary>
        /// Single-source BFS returning just the set of reached node ids (including
        /// <paramref name="source"/>), not their distances - all component-discovery needs, since
        /// it only partitions nodes into components rather than reading per-node distances. Takes a
        /// reusable <paramref name="scratch"/> (see <see cref="BFSReachScratch"/>) rather than
        /// plain <see cref="BFS"/>'s own freshly-allocated, always-cleared <c>int[] dist</c>: that
        /// clearing costs O(n) every call regardless of how small the reached set turns out to be,
        /// which is fine when a source typically reaches most of the graph, but means component
        /// discovery - which runs BFS from every yet-unvisited seed, and on a graph fragmented into
        /// many small components, most seeds only reach a handful of nodes - would otherwise pay a
        /// near-quadratic total cost instead of one close to linear in the edges actually examined.
        /// The reached list here is built incrementally as nodes are discovered during traversal,
        /// never by scanning the full node universe afterward, so the whole call is bounded by the
        /// reached set's own size plus the edges examined - never by <paramref name="nodeIds"/>'s
        /// length.
        /// </summary>
        internal static List<uint> BFSReachedNodes(uint source, BFSReachScratch scratch, Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            scratch.StartCall(nodeIds.Length);
            int[] stamp = scratch.Stamp;
            int myStamp = scratch.CurrentStamp;

            var reached = new List<uint>();
            var queue = new Queue<int>();

            int sourceIdx = nodeIndex[source];
            stamp[sourceIdx] = myStamp;
            reached.Add(source);
            queue.Enqueue(sourceIdx);

            var dynVisited = Array.ConvertAll(twoModesDynamic.ToArray(),
                _ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance));
            var statVisited = Array.ConvertAll(twoModesStatic.ToArray(), _ => new HashSet<int>());

            void VisitOneModeAlter(uint v)
            {
                if (nodeIndex.TryGetValue(v, out int vIdx) && stamp[vIdx] != myStamp)
                {
                    stamp[vIdx] = myStamp;
                    reached.Add(v);
                    queue.Enqueue(vIdx);
                }
            }
            Action<uint> visitOneModeAlter = VisitOneModeAlter;

            while (queue.Count > 0)
            {
                int uIdx = queue.Dequeue();
                uint u = nodeIds[uIdx];

                foreach (var layer in oneModes)
                    layer.VisitAlters(u, traversal, visitOneModeAlter);

                for (int li = 0; li < twoModesDynamic.Count; li++)
                {
                    var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(u);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                        if (dynVisited[li].Add(he))
                            foreach (uint m in he.NodeIds)
                                if (nodeIndex.TryGetValue(m, out int mIdx) && stamp[mIdx] != myStamp)
                                {
                                    stamp[mIdx] = myStamp;
                                    reached.Add(m);
                                    queue.Enqueue(mIdx);
                                }
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
                            if (nodeIndex.TryGetValue(m, out int mIdx) && stamp[mIdx] != myStamp)
                            {
                                stamp[mIdx] = myStamp;
                                reached.Add(m);
                                queue.Enqueue(mIdx);
                            }
                        }
                    }
                }
            }
            return reached;
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