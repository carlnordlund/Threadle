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
        /// AccumulateEdgeBetweennessForSources) reset existing state instead of allocating fresh
        /// collections on every single call. External benchmarking found betweenness scaled poorly
        /// across threads; this per-source allocation churn - happening inside the parallel loop
        /// body, competing threads for the GC - was the confirmed cause. <see cref="Dist"/>/
        /// <see cref="Sigma"/>/<see cref="Order"/> are array-backed and generation-stamped (see
        /// <see cref="Reset"/>), so they cost no per-source allocation or O(capacity) clear at all;
        /// <see cref="Pred"/> is unchanged for now (still a <c>Dictionary&lt;uint, List&lt;uint&gt;&gt;</c>
        /// allocating a fresh <c>List&lt;uint&gt;</c> per newly-discovered node) - removing that is a
        /// separate, later piece of work. One instance is created per thread (via Parallel.For's
        /// local-state factory) and reused for every source that thread ends up handling.
        /// </summary>
        internal sealed class BFSBrandesScratch
        {
            private int[] _dist = [];
            private long[] _sigma = [];
            private int[] _stamp = [];
            private int _currentStamp;
            private int[] _order = [];      // compact indices; doubles as the BFS queue (head
                                            // pointer while filling, see BFSBrandes) and, once the
                                            // forward pass finishes, the exact backward-pass order
                                            // read in reverse - no separate Stack<uint>/Queue<uint>.
            private int _orderCount;

            internal readonly Dictionary<uint, List<uint>> Pred = [];

            private Dictionary<Hyperedge, int>[] _dynExpanded = [];
            private Dictionary<int, int>[] _statExpanded = [];

            internal int[] Dist => _dist;
            internal long[] Sigma => _sigma;
            internal int[] Stamp => _stamp;
            internal int CurrentStamp => _currentStamp;
            internal int[] Order => _order;
            internal int OrderCount { get => _orderCount; set => _orderCount = value; }
            internal Dictionary<Hyperedge, int>[] DynExpanded => _dynExpanded;
            internal Dictionary<int, int>[] StatExpanded => _statExpanded;

            /// <summary>Resets all state for a new source node. <see cref="Dist"/>/<see cref="Sigma"/>/
            /// <see cref="Stamp"/>/<see cref="Order"/> are grown only the first time (or if the node
            /// count changed) - a generation stamp (<see cref="CurrentStamp"/>) marks which entries are
            /// valid for the current call instead of clearing the arrays each time. <see cref="Pred"/>
            /// and the per-2-mode-layer expansion-tracking arrays are unchanged from before.</summary>
            internal void Reset(uint source, Dictionary<uint, int> nodeIndex, int n, int dynCount, int statCount)
            {
                if (_dist.Length != n)
                {
                    _dist = new int[n];
                    _sigma = new long[n];
                    _stamp = new int[n];
                    _order = new int[n];
                }
                _currentStamp++;
                _orderCount = 0;
                int sourceIdx = nodeIndex[source];
                _stamp[sourceIdx] = _currentStamp;
                _dist[sourceIdx] = 0;
                _sigma[sourceIdx] = 1L;
                _order[_orderCount++] = sourceIdx;

                Pred.Clear();

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
        /// Read-only, per-operation precomputation for the scratch-based <see cref="BFS(uint, BFSContext, BFSScratch)"/>:
        /// built once by a caller that runs many BFS calls over the same node universe and layers (one per
        /// source node, across worker threads), and shared by all of them. For every packed (static) 2-mode
        /// layer it re-expresses the layer's two CSR structures in the operation's compact 0..n-1 node numbering
        /// (<see cref="NodeIndex"/>): each node's hyperedges as one flat array, and each hyperedge's members
        /// as compact indices (-1 for a member outside the node universe, e.g. outside the largest component).
        /// The BFS inner loop then works on plain arrays, with no dictionary lookup per node or member - the
        /// plain <see cref="BFS(uint, Dictionary{uint, int}, uint[], List{ILayerOneMode}, List{LayerTwoMode}, List{LayerTwoModeStatic}, EdgeTraversal)"/>
        /// does one lookup per dequeued node (to find its hyperedges) and one per member scanned. Costs two
        /// int arrays of the layer's total affiliation count plus one of n+1 and one of (hyperedges+1), per
        /// static 2-mode layer, for the duration of the operation. 1-mode and unpacked 2-mode layers are
        /// traversed exactly as in the plain BFS.
        /// </summary>
        internal sealed class BFSContext
        {
            internal readonly Dictionary<uint, int> NodeIndex;
            internal readonly uint[] NodeIds;
            internal readonly List<ILayerOneMode> OneModes;
            internal readonly List<LayerTwoMode> TwoModesDynamic;
            internal readonly EdgeTraversal Traversal;
            internal readonly StaticTwoModeIndex[] TwoModesStatic;

            /// <summary>One static 2-mode layer, re-indexed into the operation's compact node numbering.</summary>
            internal sealed class StaticTwoModeIndex
            {
                /// <summary>Node i's hyperedges are NodeHyperedges[NodeOffsets[i] .. NodeOffsets[i+1]).</summary>
                internal int[] NodeOffsets = [];
                internal int[] NodeHyperedges = [];
                /// <summary>Hyperedge h's members are HyperedgeMembers[HyperedgeOffsets[h] .. HyperedgeOffsets[h+1]), as compact node indices or -1.</summary>
                internal int[] HyperedgeOffsets = [];
                internal int[] HyperedgeMembers = [];
                internal int HyperedgeCount;
            }

            private BFSContext(Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, StaticTwoModeIndex[] twoModesStatic, EdgeTraversal traversal)
            {
                NodeIndex = nodeIndex;
                NodeIds = nodeIds;
                OneModes = oneModes;
                TwoModesDynamic = twoModesDynamic;
                TwoModesStatic = twoModesStatic;
                Traversal = traversal;
            }

            /// <summary>
            /// Builds the context for one operation. <paramref name="nodeIndex"/> must map every id in
            /// <paramref name="nodeIds"/> to its position there (the same pair the plain BFS takes).
            /// </summary>
            internal static BFSContext Create(Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
            {
                int n = nodeIds.Length;
                var statics = new StaticTwoModeIndex[twoModesStatic.Count];
                for (int li = 0; li < twoModesStatic.Count; li++)
                {
                    LayerTwoModeStatic layer = twoModesStatic[li];
                    var idx = new StaticTwoModeIndex();

                    // Node -> hyperedges, in compact node order
                    int[] nodeOffsets = new int[n + 1];
                    for (int i = 0; i < n; i++)
                        nodeOffsets[i + 1] = nodeOffsets[i] + (layer.TryGetNodeHyperedgeRange(nodeIds[i], out int s, out int e) ? e - s : 0);
                    int[] nodeHyperedges = new int[nodeOffsets[n]];
                    for (int i = 0; i < n; i++)
                    {
                        if (!layer.TryGetNodeHyperedgeRange(nodeIds[i], out int s, out int e))
                            continue;
                        int w = nodeOffsets[i];
                        for (int k = s; k < e; k++)
                            nodeHyperedges[w++] = layer.GetNodeHyperedgeIndex(k);
                    }

                    // Hyperedge -> members, as compact node indices (-1 if outside the node universe)
                    int h = (int)layer.NbrHyperedges;
                    int[] hyperedgeOffsets = new int[h + 1];
                    for (int he = 0; he < h; he++)
                    {
                        layer.GetHyperedgeRange(he, out int s, out int e);
                        hyperedgeOffsets[he + 1] = hyperedgeOffsets[he] + (e - s);
                    }
                    int[] hyperedgeMembers = new int[hyperedgeOffsets[h]];
                    for (int he = 0; he < h; he++)
                    {
                        layer.GetHyperedgeRange(he, out int s, out int e);
                        int w = hyperedgeOffsets[he];
                        for (int j = s; j < e; j++)
                            hyperedgeMembers[w++] = nodeIndex.TryGetValue(layer.GetHyperedgeNodeAt(j), out int m) ? m : -1;
                    }

                    idx.NodeOffsets = nodeOffsets;
                    idx.NodeHyperedges = nodeHyperedges;
                    idx.HyperedgeOffsets = hyperedgeOffsets;
                    idx.HyperedgeMembers = hyperedgeMembers;
                    idx.HyperedgeCount = h;
                    statics[li] = idx;
                }
                return new BFSContext(nodeIndex, nodeIds, oneModes, twoModesDynamic, statics, traversal);
            }
        }

        /// <summary>
        /// Reusable per-thread state for <see cref="BFS(uint, BFSContext, BFSScratch)"/>: the distance array
        /// and queue (allocated once, not per call), a visited-stamp array per static 2-mode layer (a hyperedge
        /// counts as visited in the current call when its stamp equals the current generation, so starting a
        /// new call is one increment instead of clearing or allocating a set), and a visited set per unpacked
        /// 2-mode layer (cleared, not reallocated). Create one per worker thread, e.g. via Parallel.For's
        /// local-state factory, and never share one between threads. The distances a call returns live in
        /// this scratch's array, so they are only valid until the next call with the same scratch.
        /// </summary>
        internal sealed class BFSScratch
        {
            internal int[] Dist = [];
            internal int[] Queue = [];
            internal int[][] HyperedgeStamp = [];
            internal int Generation;
            internal HashSet<Hyperedge>[] DynVisited = [];
            private BFSContext? _context;

            // State read by the 1-mode visitor below, which is created once per scratch rather than per call
            private int _queueTail;
            private int _nextDist;
            private Dictionary<uint, int> _nodeIndex = [];
            internal readonly Action<uint> VisitOneModeAlter;

            internal BFSScratch()
            {
                VisitOneModeAlter = v =>
                {
                    if (_nodeIndex.TryGetValue(v, out int vIdx) && Dist[vIdx] < 0)
                    {
                        Dist[vIdx] = _nextDist;
                        Queue[_queueTail++] = vIdx;
                    }
                };
            }

            internal int QueueTail { get => _queueTail; set => _queueTail = value; }
            internal int NextDist { set => _nextDist = value; }

            /// <summary>Prepares for a call with the given context: sizes the arrays on first use (or if the context changed), resets per-call state.</summary>
            internal void StartCall(BFSContext context)
            {
                if (!ReferenceEquals(_context, context))
                {
                    _context = context;
                    _nodeIndex = context.NodeIndex;
                    int n = context.NodeIds.Length;
                    Dist = new int[n];
                    Queue = new int[n];
                    HyperedgeStamp = new int[context.TwoModesStatic.Length][];
                    for (int li = 0; li < HyperedgeStamp.Length; li++)
                        HyperedgeStamp[li] = new int[context.TwoModesStatic[li].HyperedgeCount];
                    Generation = 0;
                    DynVisited = new HashSet<Hyperedge>[context.TwoModesDynamic.Count];
                    for (int li = 0; li < DynVisited.Length; li++)
                        DynVisited[li] = new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance);
                }
                Array.Fill(Dist, -1);
                _queueTail = 0;
                if (Generation == int.MaxValue)
                {
                    foreach (int[] stamps in HyperedgeStamp)
                        Array.Clear(stamps);
                    Generation = 0;
                }
                Generation++;
                foreach (var set in DynVisited)
                    set.Clear();
            }
        }

        /// <summary>
        /// Single-source BFS, identical in result to the plain
        /// <see cref="BFS(uint, Dictionary{uint, int}, uint[], List{ILayerOneMode}, List{LayerTwoMode}, List{LayerTwoModeStatic}, EdgeTraversal)"/>
        /// (same reached nodes, same distances; the same 1-mode, unpacked 2-mode and packed 2-mode traversal
        /// rules, each hyperedge expanded at most once), but for callers that run many BFS calls in one
        /// operation: it uses a shared, precomputed <paramref name="context"/> and a per-thread reusable
        /// <paramref name="scratch"/>, so a call allocates nothing and the packed 2-mode inner loop does no
        /// dictionary lookups. The returned distances live in <paramref name="scratch"/>'s array and are only
        /// valid until the next call with the same scratch - consume them before calling again. Callers that
        /// need to keep several BFS results at once use the plain BFS instead.
        /// </summary>
        internal static BFSDistances BFS(uint source, BFSContext context, BFSScratch scratch)
        {
            scratch.StartCall(context);
            int[] dist = scratch.Dist;
            int[] queue = scratch.Queue;
            int gen = scratch.Generation;
            uint[] nodeIds = context.NodeIds;

            int sourceIdx = context.NodeIndex[source];
            dist[sourceIdx] = 0;
            queue[0] = sourceIdx;
            scratch.QueueTail = 1;
            int head = 0;

            var oneModes = context.OneModes;
            var twoModesDynamic = context.TwoModesDynamic;
            var statics = context.TwoModesStatic;
            var nodeIndex = context.NodeIndex;

            while (head < scratch.QueueTail)
            {
                int uIdx = queue[head++];
                int next = dist[uIdx] + 1;

                if (oneModes.Count > 0)
                {
                    scratch.NextDist = next;
                    uint u = nodeIds[uIdx];
                    foreach (var layer in oneModes)
                        layer.VisitAlters(u, context.Traversal, scratch.VisitOneModeAlter);
                }

                int tail = scratch.QueueTail;
                for (int li = 0; li < twoModesDynamic.Count; li++)
                {
                    var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(nodeIds[uIdx]);
                    if (hec == null) continue;
                    var visited = scratch.DynVisited[li];
                    foreach (var he in hec.HyperEdges)
                        if (visited.Add(he))
                            foreach (uint m in he.NodeIds)
                                if (nodeIndex.TryGetValue(m, out int mIdx) && dist[mIdx] < 0)
                                {
                                    dist[mIdx] = next;
                                    queue[tail++] = mIdx;
                                }
                }

                for (int li = 0; li < statics.Length; li++)
                {
                    var st = statics[li];
                    int[] stamp = scratch.HyperedgeStamp[li];
                    int[] nodeHyperedges = st.NodeHyperedges;
                    int[] hyperedgeOffsets = st.HyperedgeOffsets;
                    int[] members = st.HyperedgeMembers;
                    for (int k = st.NodeOffsets[uIdx], kEnd = st.NodeOffsets[uIdx + 1]; k < kEnd; k++)
                    {
                        int h = nodeHyperedges[k];
                        if (stamp[h] == gen) continue;
                        stamp[h] = gen;
                        for (int j = hyperedgeOffsets[h], jEnd = hyperedgeOffsets[h + 1]; j < jEnd; j++)
                        {
                            int mIdx = members[j];
                            if (mIdx >= 0 && dist[mIdx] < 0)
                            {
                                dist[mIdx] = next;
                                queue[tail++] = mIdx;
                            }
                        }
                    }
                }
                scratch.QueueTail = tail;
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
        /// after this returns. <paramref name="nodeIndex"/>/<paramref name="nodeIds"/> are the same
        /// compact 0..n-1 numbering described on <see cref="BFS"/>, built once by the caller.
        /// </summary>
        internal static void BFSBrandes(uint source, Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal, BFSBrandesScratch scratch)
        {
            scratch.Reset(source, nodeIndex, nodeIds.Length, twoModesDynamic.Count, twoModesStatic.Count);
            int[] dist = scratch.Dist;
            long[] sigma = scratch.Sigma;
            int[] stamp = scratch.Stamp;
            int myStamp = scratch.CurrentStamp;
            int[] order = scratch.Order;
            int orderCount = scratch.OrderCount; // starts at 1: Reset() already appended the source
            var pred = scratch.Pred;
            var dynExpanded = scratch.DynExpanded;
            var statExpanded = scratch.StatExpanded;

            int head = 0; // dequeue pointer into order[] - order[] doubles as the BFS queue here
                          // (append at orderCount, dequeue at head) and, once this forward pass
                          // finishes, as the exact backward-pass processing order read in reverse.

            uint currentU = source;
            int currentUIdx = 0;
            int currentMemberDist = 0;

            void ProcessNeighborArr(uint v, uint u, int uIdx, int memberDist)
            {
                if (!nodeIndex.TryGetValue(v, out int vIdx)) return;
                if (stamp[vIdx] != myStamp)
                {
                    stamp[vIdx] = myStamp;
                    dist[vIdx] = memberDist;
                    sigma[vIdx] = sigma[uIdx];
                    pred[v] = [u];
                    order[orderCount++] = vIdx;
                }
                else if (dist[vIdx] == memberDist)
                {
                    sigma[vIdx] += sigma[uIdx];
                    pred[v].Add(u);
                }
            }

            void VisitOneModeAlter(uint v) => ProcessNeighborArr(v, currentU, currentUIdx, currentMemberDist);
            Action<uint> visitOneModeAlter = VisitOneModeAlter;

            while (head < orderCount)
            {
                int uIdx = order[head++];
                uint u = nodeIds[uIdx];
                int memberDist = dist[uIdx] + 1;
                currentU = u;
                currentUIdx = uIdx;
                currentMemberDist = memberDist;

                foreach (var layer in oneModes)
                {
                    // Both-on-directed needs GetNodeAlters' own dedup (see remarks above) - every
                    // other combination (Out, In, or any traversal on an undirected layer, where
                    // VisitAlters only ever takes its first branch) is safe without it.
                    if (traversal == EdgeTraversal.Both && layer.IsDirectional)
                        foreach (uint v in layer.GetNodeAlters(u, traversal))
                            ProcessNeighborArr(v, u, uIdx, memberDist);
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
                                if (m != u) ProcessNeighborArr(m, u, uIdx, memberDist);
                        }
                        else if (expandedAt == memberDist)
                        {
                            foreach (uint m in he.NodeIds)
                                if (m != u && nodeIndex.TryGetValue(m, out int mIdx) && stamp[mIdx] == myStamp && dist[mIdx] == memberDist)
                                {
                                    sigma[mIdx] += sigma[uIdx];
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
                                if (m != u) ProcessNeighborArr(m, u, uIdx, memberDist);
                            }
                        }
                        else if (expandedAt == memberDist)
                        {
                            twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                            for (int j = hStart; j < hEnd; j++)
                            {
                                uint m = twoModesStatic[li].GetHyperedgeNodeAt(j);
                                if (m != u && nodeIndex.TryGetValue(m, out int mIdx) && stamp[mIdx] == myStamp && dist[mIdx] == memberDist)
                                {
                                    sigma[mIdx] += sigma[uIdx];
                                    pred[m].Add(u);
                                }
                            }
                        }
                    }
                }
            }
            scratch.OrderCount = orderCount;
        }
        #endregion
    }
}