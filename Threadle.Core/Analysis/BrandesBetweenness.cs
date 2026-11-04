using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// Brandes' algorithm for node and edge betweenness, without per-node predecessor lists and without
    /// dictionaries on the hot path. Per source node:
    ///
    /// Forward pass (BFS): distances and shortest-path counts (sigma) in per-thread arrays, nodes recorded in
    /// discovery order (which is also BFS level order). A node one level further from the source receives the
    /// path count of each predecessor once per connection: once per 1-mode layer containing the edge (on a
    /// directed layer under 'both' traversal, an edge present in both directions counts once, as
    /// GetNodeAlters' de-duplicated union always did), and once per hyperedge the two nodes share on a 2-mode
    /// layer. For a 2-mode layer each hyperedge is handled once per BFS, when first reached from level d: its
    /// members at level d (all of which are known by then) contribute the sum of their path counts to its
    /// members at level d+1 - the same total the previous pairwise version accumulated, but in two passes over
    /// the hyperedge instead of one pass per level-d member, so a 2-mode BFS costs O(sum of hyperedge sizes)
    /// instead of up to O(sum of hyperedge sizes squared). Path counts and discovery order are exactly as before.
    ///
    /// Backward pass: nodes in reverse discovery order. A node's predecessors are not stored but re-derived:
    /// on a 1-mode layer, its neighbours in the reverse traversal direction that lie one level closer to the
    /// source (the same connections, with the same multiplicity, the forward pass used); each gets
    /// (sigma[v] / sigma[w]) * (1 + delta[w]), in the same order per v as before, so 1-mode results are
    /// bit-for-bit identical to the predecessor-list version. On a 2-mode layer, each hyperedge reached from
    /// level d passes back, at once, sum over its level-(d+1) members w of (1 + delta[w]) / sigma[w], times
    /// sigma[v], to each of its level-d members v: mathematically the same as the pairwise sum, but grouped
    /// differently, so values can differ from the pairwise version in the last few bits of a double.
    ///
    /// Nodes outside the command's node universe are ignored, exactly as before. Hyperedges are assumed not
    /// to list the same node twice (the model de-duplicates members on insertion).
    /// </summary>
    internal static class BrandesBetweenness
    {
        /// <summary>Per-command, read-only description of the layers, shared by all worker threads.</summary>
        internal sealed class Context
        {
            internal readonly Dictionary<uint, int> NodeIndex;
            internal readonly uint[] NodeIds;
            internal readonly int[]? DirectIndex;
            internal readonly EdgeTraversal Traversal;
            internal readonly EdgeTraversal ReverseTraversal;
            internal readonly OneMode[] OneModes;
            internal readonly CommunityFunctions.TwoModeIncidence[] TwoModes;

            /// <summary>A 1-mode layer: packed layers are read through their CSR arrays, unpacked ones through the layer's own methods.</summary>
            internal sealed class OneMode
            {
                internal ILayerOneMode Layer = null!;
                /// <summary>Directed layer under 'both' traversal: a neighbour reachable in both directions counts once.</summary>
                internal bool Dedup;
                // Packed layers only (LayerPos == null for an unpacked layer)
                internal int[]? LayerPos;
                internal int[] OutOffsets = [];
                internal uint[] OutNbrs = [];
                internal int[]? InOffsets;
                internal uint[]? InNbrs;
                internal bool FwdOut, FwdIn, RevOut, RevIn;
            }

            internal Context(uint[] nodeIds, Dictionary<uint, int> nodeIndex, List<ILayerOneMode> oneModes,
                List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
            {
                NodeIds = nodeIds;
                NodeIndex = nodeIndex;
                Traversal = traversal;
                ReverseTraversal = traversal switch
                {
                    EdgeTraversal.Out => EdgeTraversal.In,
                    EdgeTraversal.In => EdgeTraversal.Out,
                    _ => EdgeTraversal.Both
                };
                int n = nodeIds.Length;

                uint maxId = 0;
                foreach (uint id in nodeIds)
                    if (id > maxId) maxId = id;
                if (n > 0 && (long)maxId + 1 <= 4L * n + (1 << 20))
                {
                    DirectIndex = new int[maxId + 1];
                    Array.Fill(DirectIndex, -1);
                    for (int i = 0; i < n; i++)
                        DirectIndex[nodeIds[i]] = i;
                }

                OneModes = new OneMode[oneModes.Count];
                for (int li = 0; li < oneModes.Count; li++)
                {
                    var layer = oneModes[li];
                    var om = new OneMode { Layer = layer, Dedup = traversal == EdgeTraversal.Both && layer.IsDirectional };
                    if (layer is LayerOneModeStatic st)
                    {
                        om.LayerPos = new int[n];
                        var mapper = st.NodeIdToIndexMapper;
                        for (int i = 0; i < n; i++)
                            om.LayerPos[i] = mapper.TryGetValue(nodeIds[i], out int p) ? p : -1;
                        om.OutOffsets = st.CsrOffsets;
                        om.OutNbrs = st.CsrNeighborNodeIds;
                        om.InOffsets = st.CsrInOffsets;
                        om.InNbrs = st.CsrInNeighborNodeIds;
                        bool hasIn = om.InOffsets != null && om.InNbrs != null;
                        // Same direction rules as LayerOneModeStatic.VisitAlters / GetNodeAlters
                        om.FwdOut = !layer.IsDirectional || traversal == EdgeTraversal.Out || traversal == EdgeTraversal.Both;
                        om.FwdIn = layer.IsDirectional && (traversal == EdgeTraversal.In || traversal == EdgeTraversal.Both) && hasIn;
                        om.RevOut = !layer.IsDirectional || ReverseTraversal == EdgeTraversal.Out || ReverseTraversal == EdgeTraversal.Both;
                        om.RevIn = layer.IsDirectional && (ReverseTraversal == EdgeTraversal.In || ReverseTraversal == EdgeTraversal.Both) && hasIn;
                    }
                    OneModes[li] = om;
                }

                // Unpacked 2-mode layers first, then packed ones: the order the previous version visited them in
                var twoModes = new List<CommunityFunctions.TwoModeIncidence>();
                foreach (var l in twoModesDynamic) twoModes.Add(CommunityFunctions.TwoModeIncidence.Build(nodeIds, l));
                foreach (var l in twoModesStatic) twoModes.Add(CommunityFunctions.TwoModeIncidence.Build(nodeIds, l));
                TwoModes = twoModes.ToArray();
            }

            internal int IndexOf(uint v)
            {
                if (DirectIndex != null)
                    return v < (uint)DirectIndex.Length ? DirectIndex[v] : -1;
                return NodeIndex.TryGetValue(v, out int i) ? i : -1;
            }
        }

        /// <summary>Per-thread working state, reused for every source that thread handles.</summary>
        internal sealed class Scratch
        {
            internal int[] Dist = [];
            internal long[] Sigma = [];
            internal int[] Order = [];
            internal int OrderCount;
            internal double[] Delta = [];
            internal int[] Reached = [];        // == Gen if reached in the current BFS
            internal int[] Seen = [];           // per-node-visit de-duplication for 'both' on directed layers
            internal int SeenGen;
            internal int Gen;
            internal int[][] HyperedgeTouched = []; // == Gen if the hyperedge was reached in the current BFS
            internal int[] TouchedLayer = [];
            internal int[] TouchedHyperedge = [];
            internal int[] TouchedLevel = [];
            internal int TouchedCount;
            private Context? _context;

            // Unpacked 1-mode layers are traversed via callbacks; these fields carry the current node's state
            private int _cbUIdx;
            private int _cbNext;
            private Context _cbContext = null!;
            private int _cbW;
            private int _cbLevelBelow;
            private double _cbFactor;
            internal readonly Action<uint> ForwardVisit;
            internal readonly Action<uint> BackwardVisit;
            internal Action<int, int, double>? OnPredecessor; // (vIdx, wIdx, contribution) - edge betweenness only

            internal Scratch()
            {
                ForwardVisit = v => ForwardNeighbor(_cbContext.IndexOf(v), _cbUIdx, _cbNext);
                BackwardVisit = v => BackwardNeighbor(_cbContext.IndexOf(v), _cbW, _cbLevelBelow, _cbFactor);
            }

            internal void Start(Context context, int sourceIdx)
            {
                if (!ReferenceEquals(_context, context))
                {
                    _context = context;
                    _cbContext = context;
                    int n = context.NodeIds.Length;
                    Dist = new int[n];
                    Sigma = new long[n];
                    Order = new int[n];
                    Delta = new double[n];
                    Reached = new int[n];
                    Seen = new int[n];
                    SeenGen = 0;
                    Gen = 0;
                    HyperedgeTouched = new int[context.TwoModes.Length][];
                    int totalH = 0;
                    for (int li = 0; li < context.TwoModes.Length; li++)
                    {
                        int h = context.TwoModes[li].HyperedgeOffsets.Length - 1;
                        HyperedgeTouched[li] = new int[h];
                        totalH += h;
                    }
                    TouchedLayer = new int[totalH];
                    TouchedHyperedge = new int[totalH];
                    TouchedLevel = new int[totalH];
                }
                if (++Gen == int.MaxValue)
                {
                    Array.Clear(Reached);
                    foreach (var t in HyperedgeTouched) Array.Clear(t);
                    Gen = 1;
                }
                Reached[sourceIdx] = Gen;
                Dist[sourceIdx] = 0;
                Sigma[sourceIdx] = 1;
                Order[0] = sourceIdx;
                OrderCount = 1;
                TouchedCount = 0;
            }

            internal int NextSeenGen()
            {
                if (++SeenGen == int.MaxValue)
                {
                    Array.Clear(Seen);
                    SeenGen = 1;
                }
                return SeenGen;
            }

            /// <summary>Forward step: neighbour v (compact index, or -1) of u, at distance next.</summary>
            internal void ForwardNeighbor(int vIdx, int uIdx, int next)
            {
                if (vIdx < 0) return;
                if (Reached[vIdx] != Gen)
                {
                    Reached[vIdx] = Gen;
                    Dist[vIdx] = next;
                    Sigma[vIdx] = Sigma[uIdx];
                    Order[OrderCount++] = vIdx;
                }
                else if (Dist[vIdx] == next)
                    Sigma[vIdx] += Sigma[uIdx];
            }

            /// <summary>Backward step: v (compact index, or -1) is a predecessor of w if it lies one level closer.</summary>
            internal void BackwardNeighbor(int vIdx, int wIdx, int levelBelow, double factor)
            {
                if (vIdx < 0 || Reached[vIdx] != Gen || Dist[vIdx] != levelBelow) return;
                double contrib = (Sigma[vIdx] / (double)Sigma[wIdx]) * factor;
                Delta[vIdx] += contrib;
                OnPredecessor?.Invoke(vIdx, wIdx, contrib);
            }

            internal void SetForwardCallbackState(int uIdx, int next) { _cbUIdx = uIdx; _cbNext = next; }
            internal void SetBackwardCallbackState(int w, int levelBelow, double factor) { _cbW = w; _cbLevelBelow = levelBelow; _cbFactor = factor; }
        }

        /// <summary>Forward pass from one source: fills the scratch's distances, path counts and discovery order.</summary>
        internal static void Forward(Context ctx, Scratch s, int sourceIdx)
        {
            s.Start(ctx, sourceIdx);
            uint[] nodeIds = ctx.NodeIds;
            int gen = s.Gen;
            int head = 0;
            while (head < s.OrderCount)
            {
                int uIdx = s.Order[head++];
                uint u = nodeIds[uIdx];
                int d = s.Dist[uIdx];
                int next = d + 1;

                // 1-mode layers
                foreach (var om in ctx.OneModes)
                {
                    if (om.LayerPos == null)
                    {
                        if (om.Dedup)
                            foreach (uint v in om.Layer.GetNodeAlters(u, ctx.Traversal))
                                s.ForwardNeighbor(ctx.IndexOf(v), uIdx, next);
                        else
                        {
                            s.SetForwardCallbackState(uIdx, next);
                            om.Layer.VisitAlters(u, ctx.Traversal, s.ForwardVisit);
                        }
                        continue;
                    }
                    int p = om.LayerPos[uIdx];
                    if (p < 0) continue;
                    int seen = om.Dedup ? s.NextSeenGen() : 0;
                    if (om.FwdOut)
                        for (int j = om.OutOffsets[p], e = om.OutOffsets[p + 1]; j < e; j++)
                        {
                            int vIdx = ctx.IndexOf(om.OutNbrs[j]);
                            if (om.Dedup)
                            {
                                if (vIdx < 0 || s.Seen[vIdx] == seen) continue;
                                s.Seen[vIdx] = seen;
                            }
                            s.ForwardNeighbor(vIdx, uIdx, next);
                        }
                    if (om.FwdIn)
                        for (int j = om.InOffsets![p], e = om.InOffsets[p + 1]; j < e; j++)
                        {
                            int vIdx = ctx.IndexOf(om.InNbrs![j]);
                            if (om.Dedup)
                            {
                                if (vIdx < 0 || s.Seen[vIdx] == seen) continue;
                                s.Seen[vIdx] = seen;
                            }
                            s.ForwardNeighbor(vIdx, uIdx, next);
                        }
                }

                // 2-mode layers: each hyperedge once per BFS, when first reached
                for (int li = 0; li < ctx.TwoModes.Length; li++)
                {
                    var inc = ctx.TwoModes[li];
                    int[] touched = s.HyperedgeTouched[li];
                    for (int k = inc.NodeOffsets[uIdx], kEnd = inc.NodeOffsets[uIdx + 1]; k < kEnd; k++)
                    {
                        int h = inc.NodeHyperedges[k];
                        if (touched[h] == gen) continue;
                        touched[h] = gen;
                        s.TouchedLayer[s.TouchedCount] = li;
                        s.TouchedHyperedge[s.TouchedCount] = h;
                        s.TouchedLevel[s.TouchedCount] = d;
                        s.TouchedCount++;

                        int hs = inc.HyperedgeOffsets[h], he = inc.HyperedgeOffsets[h + 1];
                        long sum = 0; // path counts of the hyperedge's members at level d (all known by now)
                        for (int j = hs; j < he; j++)
                        {
                            int m = inc.Members[j];
                            if (m >= 0 && s.Reached[m] == gen && s.Dist[m] == d)
                                sum += s.Sigma[m];
                        }
                        for (int j = hs; j < he; j++)
                        {
                            int m = inc.Members[j];
                            if (m < 0) continue;
                            if (s.Reached[m] != gen)
                            {
                                s.Reached[m] = gen;
                                s.Dist[m] = next;
                                s.Sigma[m] = sum;
                                s.Order[s.OrderCount++] = m;
                            }
                            else if (s.Dist[m] == next)
                                s.Sigma[m] += sum;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Backward pass after <see cref="Forward"/>: accumulates delta for every reached node and, if
        /// <paramref name="betweenness"/> is given, adds each reached non-source node's delta to it.
        /// </summary>
        internal static void Backward(Context ctx, Scratch s, int sourceIdx, double[]? betweenness)
        {
            int gen = s.Gen;
            double[] delta = s.Delta;
            for (int i = 0; i < s.OrderCount; i++)
                delta[s.Order[i]] = 0.0;

            int t = s.TouchedCount - 1;
            int idx = s.OrderCount - 1;
            while (idx >= 0)
            {
                int level = s.Dist[s.Order[idx]];

                // Hyperedges reached from this level pass credit back from their members one level further out
                while (t >= 0 && s.TouchedLevel[t] == level)
                {
                    var inc = ctx.TwoModes[s.TouchedLayer[t]];
                    int h = s.TouchedHyperedge[t];
                    int hs = inc.HyperedgeOffsets[h], he = inc.HyperedgeOffsets[h + 1];
                    double sum = 0.0;
                    for (int j = hs; j < he; j++)
                    {
                        int m = inc.Members[j];
                        if (m >= 0 && s.Reached[m] == gen && s.Dist[m] == level + 1)
                            sum += (1.0 + delta[m]) / s.Sigma[m];
                    }
                    if (sum != 0.0)
                        for (int j = hs; j < he; j++)
                        {
                            int m = inc.Members[j];
                            if (m >= 0 && s.Reached[m] == gen && s.Dist[m] == level)
                                delta[m] += s.Sigma[m] * sum;
                        }
                    t--;
                }

                // Nodes at this level: delta is final; pass credit back along 1-mode edges
                for (; idx >= 0 && s.Dist[s.Order[idx]] == level; idx--)
                {
                    int w = s.Order[idx];
                    if (w == sourceIdx) continue;
                    double factor = 1.0 + delta[w];
                    int below = level - 1;
                    uint wId = ctx.NodeIds[w];
                    foreach (var om in ctx.OneModes)
                    {
                        if (om.LayerPos == null)
                        {
                            if (om.Dedup)
                                foreach (uint v in om.Layer.GetNodeAlters(wId, ctx.ReverseTraversal))
                                    s.BackwardNeighbor(ctx.IndexOf(v), w, below, factor);
                            else
                            {
                                s.SetBackwardCallbackState(w, below, factor);
                                om.Layer.VisitAlters(wId, ctx.ReverseTraversal, s.BackwardVisit);
                            }
                            continue;
                        }
                        int p = om.LayerPos[w];
                        if (p < 0) continue;
                        int seen = om.Dedup ? s.NextSeenGen() : 0;
                        if (om.RevOut)
                            for (int j = om.OutOffsets[p], e = om.OutOffsets[p + 1]; j < e; j++)
                            {
                                int vIdx = ctx.IndexOf(om.OutNbrs[j]);
                                if (om.Dedup)
                                {
                                    if (vIdx < 0 || s.Seen[vIdx] == seen) continue;
                                    s.Seen[vIdx] = seen;
                                }
                                s.BackwardNeighbor(vIdx, w, below, factor);
                            }
                        if (om.RevIn)
                            for (int j = om.InOffsets![p], e = om.InOffsets[p + 1]; j < e; j++)
                            {
                                int vIdx = ctx.IndexOf(om.InNbrs![j]);
                                if (om.Dedup)
                                {
                                    if (vIdx < 0 || s.Seen[vIdx] == seen) continue;
                                    s.Seen[vIdx] = seen;
                                }
                                s.BackwardNeighbor(vIdx, w, below, factor);
                            }
                    }
                    if (betweenness != null)
                        betweenness[w] += delta[w];
                }
            }
        }

        /// <summary>
        /// Raw (unnormalized) node betweenness summed over the given sources, in parallel across up to
        /// 'maxthreads' threads; each thread sums into its own array, merged under a lock. Returns one entry
        /// per node of <paramref name="nodeIds"/>.
        /// </summary>
        internal static Dictionary<uint, double> NodeBetweenness(uint[] sources, uint[] nodeIds, Dictionary<uint, int> nodeIndex,
            List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            int n = nodeIds.Length;
            var total = new double[n];
            if (sources.Length > 0)
            {
                var ctx = new Context(nodeIds, nodeIndex, oneModes, twoModesDynamic, twoModesStatic, traversal);
                var mergeLock = new object();
                System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(),
                    () => (Scratch: new Scratch(), Sum: new double[n]),
                    (i, loopState, local) =>
                    {
                        int src = nodeIndex[sources[i]];
                        Forward(ctx, local.Scratch, src);
                        Backward(ctx, local.Scratch, src, local.Sum);
                        return local;
                    },
                    local =>
                    {
                        lock (mergeLock)
                        {
                            for (int v = 0; v < n; v++)
                                total[v] += local.Sum[v];
                        }
                    });
            }
            var result = new Dictionary<uint, double>(n);
            for (int v = 0; v < n; v++)
                result[nodeIds[v]] = total[v];
            return result;
        }

        /// <summary>
        /// Raw (unnormalized) edge betweenness for a single 1-mode layer, summed over the given sources, in
        /// parallel; keyed as before ((from, to) for directed layers, (smaller, larger) id for undirected ones).
        /// Each predecessor edge (v, w) on a shortest-path DAG gets the same contribution node betweenness folds
        /// into delta[v].
        /// </summary>
        internal static Dictionary<(uint From, uint To), double> EdgeBetweenness(uint[] sources, uint[] nodeIds, Dictionary<uint, int> nodeIndex,
            ILayerOneMode layer, EdgeTraversal traversal)
        {
            var edgeBetweenness = new Dictionary<(uint From, uint To), double>();
            if (sources.Length == 0)
                return edgeBetweenness;
            var ctx = new Context(nodeIds, nodeIndex, [layer], [], [], traversal);
            bool directed = layer.IsDirectional;
            var mergeLock = new object();
            System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(),
                () =>
                {
                    var local = new Dictionary<(uint From, uint To), double>();
                    var scratch = new Scratch();
                    scratch.OnPredecessor = (vIdx, wIdx, contrib) =>
                    {
                        uint v = nodeIds[vIdx], w = nodeIds[wIdx];
                        var key = (directed || v < w) ? (v, w) : (w, v);
                        local[key] = local.GetValueOrDefault(key) + contrib;
                    };
                    return (Scratch: scratch, Local: local);
                },
                (i, loopState, local) =>
                {
                    int src = nodeIndex[sources[i]];
                    Forward(ctx, local.Scratch, src);
                    Backward(ctx, local.Scratch, src, null);
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (key, value) in local.Local)
                            edgeBetweenness[key] = edgeBetweenness.GetValueOrDefault(key) + value;
                    }
                });
            return edgeBetweenness;
        }
    }
}