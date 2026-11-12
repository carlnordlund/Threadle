using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    internal static class PathFunctions
    {
        /// <summary>
        /// Bidirectional BFS for a single source-target pair. Expands the smaller frontier one level at a
        /// time and stops at the first node reached from both sides, or - no path - as soon as either
        /// side has nothing left to expand. That is exact: if a level's expansion
        /// reaches a node the other side has already reached, every such meeting in that level gives the
        /// same total length (a shorter one would have been found, and the search stopped, a level
        /// earlier), and the first one found is the one kept - so stopping there gives the same distance
        /// and the same path as finishing the level. Hyperedge-aware: per-direction visited sets ensure each
        /// 2-mode hyperedge is expanded at most once per direction. Packed 1-mode layers are read straight
        /// from their neighbour arrays (same neighbours, same order as GetNodeAlters, without a copy per
        /// node).
        /// </summary>
        internal static ShortestPathResult BidirectionalBFS(uint source, uint target, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, bool returnPath)
        {
            var fwd = new Side(source, twoModesDynamic.Count, twoModesStatic.Count, returnPath, inbound: false);
            var bwd = new Side(target, twoModesDynamic.Count, twoModesStatic.Count, returnPath, inbound: true);
            int best = int.MaxValue;
            uint meetingNode = 0;

            // Once either side's frontier is empty, that side has reached everything it can without
            // meeting the other, so there is no path (the old loop kept expanding the other side, which on
            // e.g. an isolated node meant traversing the whole rest of its component for nothing).
            while (fwd.Frontier.Count > 0 && bwd.Frontier.Count > 0)
            {
                bool forward = fwd.Frontier.Count <= bwd.Frontier.Count;
                Side side = forward ? fwd : bwd, other = forward ? bwd : fwd;
                if (Expand(side, other, oneModes, twoModesDynamic, twoModesStatic, out meetingNode, out int otherDist))
                {
                    best = side.Level + otherDist;
                    break;
                }
            }

            var predFwd = fwd.Pred;
            var predBwd = bwd.Pred;
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

        /// <summary>
        /// Per-thread reusable state for <see cref="BidirectionalBFS(int, int, GraphAlgorithms.BFSContext, GraphAlgorithms.BFSContext, BidirScratch, bool)"/>:
        /// per-node distance, predecessor and visit-stamp arrays for each direction (a node counts as reached in
        /// the current search only if its stamp equals the current generation, so nothing is cleared between
        /// pairs), per-hyperedge stamps for packed 2-mode layers, and reusable frontier lists.
        /// </summary>
        internal sealed class BidirScratch
        {
            internal int[] StampF = [], StampB = [], DistF = [], DistB = [], PredF = [], PredB = [];
            internal int[][] HStampF = [], HStampB = [];
            internal HashSet<Hyperedge>[] DynF = [], DynB = [];
            internal List<int> FrontF = [], FrontB = [], NextF = [], NextB = [];
            internal int Gen;
            private GraphAlgorithms.BFSContext? _context;

            internal void Start(GraphAlgorithms.BFSContext context)
            {
                if (!ReferenceEquals(_context, context))
                {
                    _context = context;
                    int n = context.NodeIds.Length;
                    StampF = new int[n]; StampB = new int[n];
                    DistF = new int[n]; DistB = new int[n];
                    PredF = new int[n]; PredB = new int[n];
                    HStampF = context.TwoModesStatic.Select(st => new int[st.HyperedgeCount]).ToArray();
                    HStampB = context.TwoModesStatic.Select(st => new int[st.HyperedgeCount]).ToArray();
                    DynF = context.TwoModesDynamic.Select(_ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance)).ToArray();
                    DynB = context.TwoModesDynamic.Select(_ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance)).ToArray();
                    Gen = 0;
                }
                if (++Gen == int.MaxValue)
                {
                    Array.Clear(StampF); Array.Clear(StampB);
                    foreach (var a in HStampF) Array.Clear(a);
                    foreach (var a in HStampB) Array.Clear(a);
                    Gen = 1;
                }
                foreach (var d in DynF) d.Clear();
                foreach (var d in DynB) d.Clear();
                FrontF.Clear(); FrontB.Clear();
            }
        }

        /// <summary>
        /// Array-based bidirectional BFS for one pair (compact node indices) of a batch, using per-command contexts
        /// for the forward (Out) and backward (In) directions and per-thread reusable scratch. Same neighbour
        /// order, same frontier choice and the same early stops as the per-pair version above, so it gives the
        /// same distance and the same path; only the bookkeeping is arrays instead of hash maps.
        /// </summary>
        internal static ShortestPathResult BidirectionalBFS(int source, int target, GraphAlgorithms.BFSContext fwd, GraphAlgorithms.BFSContext bwd, BidirScratch sc, bool returnPath)
        {
            sc.Start(fwd);
            int gen = sc.Gen;
            uint[] nodeIds = fwd.NodeIds;
            sc.StampF[source] = gen; sc.DistF[source] = 0; sc.PredF[source] = -1;
            sc.StampB[target] = gen; sc.DistB[target] = 0; sc.PredB[target] = -1;
            sc.FrontF.Add(source);
            sc.FrontB.Add(target);
            int levelF = 0, levelB = 0;
            int meeting = -1, best = -1;

            while (sc.FrontF.Count > 0 && sc.FrontB.Count > 0)
            {
                bool forward = sc.FrontF.Count <= sc.FrontB.Count;
                if (forward)
                {
                    levelF++;
                    sc.NextF.Clear();
                    if (ExpandArrays(fwd, sc.FrontF, sc.NextF, levelF, sc.StampF, sc.DistF, sc.PredF, sc.HStampF, sc.DynF, sc.StampB, sc.DistB, gen, out meeting, out int otherDist))
                    { best = levelF + otherDist; break; }
                    (sc.FrontF, sc.NextF) = (sc.NextF, sc.FrontF);
                }
                else
                {
                    levelB++;
                    sc.NextB.Clear();
                    if (ExpandArrays(bwd, sc.FrontB, sc.NextB, levelB, sc.StampB, sc.DistB, sc.PredB, sc.HStampB, sc.DynB, sc.StampF, sc.DistF, gen, out meeting, out int otherDist))
                    { best = levelB + otherDist; break; }
                    (sc.FrontB, sc.NextB) = (sc.NextB, sc.FrontB);
                }
            }

            if (best < 0)
                return new ShortestPathResult(-1, null);
            uint[]? path = null;
            if (returnPath)
            {
                var list = new List<uint>(best + 1);
                for (int cur = meeting; cur != -1; cur = sc.PredF[cur])
                    list.Add(nodeIds[cur]);
                list.Reverse();
                for (int cur = sc.PredB[meeting]; cur != -1; cur = sc.PredB[cur])
                    list.Add(nodeIds[cur]);
                path = [.. list];
            }
            return new ShortestPathResult(best, path);
        }

        /// <summary>
        /// One level of one direction for the array-based search: 1-mode layers in order (packed ones read from
        /// their CSR arrays, unpacked ones through GetNodeAlters, as the per-pair version), then dynamic and
        /// packed 2-mode layers. Returns true at the first newly reached node the other side has reached.
        /// </summary>
        private static bool ExpandArrays(GraphAlgorithms.BFSContext ctx, List<int> frontier, List<int> next, int level,
            int[] stamp, int[] dist, int[] pred, int[][] hStamp, HashSet<Hyperedge>[] dyn, int[] otherStamp, int[] otherDist, int gen,
            out int meeting, out int otherDistance)
        {
            meeting = -1;
            otherDistance = 0;
            uint[] nodeIds = ctx.NodeIds;
            int[]? direct = ctx.DirectIndex;
            uint directBase = ctx.DirectBase;
            var nodeIndex = ctx.NodeIndex;
            int IndexOf(uint v) => direct != null ? (v - directBase < (uint)direct.Length ? direct[v - directBase] : -1) : (nodeIndex.TryGetValue(v, out int i) ? i : -1);

            foreach (int u in frontier)
            {
                for (int li = 0; li < ctx.OneModes.Count; li++)
                {
                    var fast = ctx.OneModesStatic[li];
                    if (fast == null)
                    {
                        foreach (uint v in ctx.OneModes[li].GetNodeAlters(nodeIds[u], ctx.Traversal))
                            if (Reach(IndexOf(v), u, out meeting, out otherDistance)) return true;
                        continue;
                    }
                    int p = fast.LayerPos[u];
                    if (p < 0) continue;
                    if (fast.UseOut)
                    {
                        uint[] nb = fast.Neighbors;
                        for (int j = fast.Offsets[p], jEnd = fast.Offsets[p + 1]; j < jEnd; j++)
                            if (Reach(IndexOf(nb[j]), u, out meeting, out otherDistance)) return true;
                    }
                    if (fast.UseIn)
                    {
                        uint[] nb = fast.InNeighbors!;
                        int[] off = fast.InOffsets!;
                        for (int j = off[p], jEnd = off[p + 1]; j < jEnd; j++)
                            if (Reach(IndexOf(nb[j]), u, out meeting, out otherDistance)) return true;
                    }
                }
                for (int li = 0; li < ctx.TwoModesDynamic.Count; li++)
                {
                    var hec = ctx.TwoModesDynamic[li].GetNonEmptyHyperedgeCollection(nodeIds[u]);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                        if (dyn[li].Add(he))
                            foreach (uint m in he.NodeIds)
                                if (Reach(IndexOf(m), u, out meeting, out otherDistance)) return true;
                }
                for (int li = 0; li < ctx.TwoModesStatic.Length; li++)
                {
                    var st = ctx.TwoModesStatic[li];
                    int[] hs = hStamp[li];
                    for (int k = st.NodeOffsets[u], kEnd = st.NodeOffsets[u + 1]; k < kEnd; k++)
                    {
                        int h = st.NodeHyperedges[k];
                        if (hs[h] == gen) continue;
                        hs[h] = gen;
                        for (int j = st.HyperedgeOffsets[h], jEnd = st.HyperedgeOffsets[h + 1]; j < jEnd; j++)
                            if (Reach(st.HyperedgeMembers[j], u, out meeting, out otherDistance)) return true;
                    }
                }
            }
            return false;

            bool Reach(int v, int u, out int meet, out int od)
            {
                meet = -1;
                od = 0;
                if (v < 0 || stamp[v] == gen)
                    return false;
                stamp[v] = gen;
                dist[v] = level;
                pred[v] = u;
                next.Add(v);
                if (otherStamp[v] != gen)
                    return false;
                meet = v;
                od = otherDist[v];
                return true;
            }
        }

        /// <summary>One direction's search state: distances, predecessors (if paths are wanted), current frontier.</summary>
        private sealed class Side
        {
            internal readonly Dictionary<uint, int> Dist;
            internal readonly Dictionary<uint, uint>? Pred;
            internal List<uint> Frontier;
            internal int Level;
            internal readonly bool Inbound;
            internal readonly HashSet<Hyperedge>[] DynVisited;
            internal readonly HashSet<int>[] StatVisited;

            internal Side(uint start, int nDyn, int nStat, bool returnPath, bool inbound)
            {
                Dist = new Dictionary<uint, int> { [start] = 0 };
                Pred = returnPath ? [] : null;
                Frontier = [start];
                Inbound = inbound;
                DynVisited = new HashSet<Hyperedge>[nDyn];
                for (int i = 0; i < nDyn; i++) DynVisited[i] = new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance);
                StatVisited = new HashSet<int>[nStat];
                for (int i = 0; i < nStat; i++) StatVisited[i] = [];
            }

            /// <summary>
            /// Records v as reached from u at the current level, unless already reached. Returns true if v
            /// is new and the other side has also reached it (otherDist = its distance there).
            /// </summary>
            internal bool Reach(uint v, uint u, List<uint> next, Side other, out int otherDist)
            {
                otherDist = 0;
                if (!Dist.TryAdd(v, Level))
                    return false;
                next.Add(v);
                if (Pred != null) Pred[v] = u;
                return other.Dist.TryGetValue(v, out otherDist);
            }
        }

        /// <summary>
        /// Expands one level of <paramref name="side"/>: every frontier node's neighbours in each 1-mode
        /// layer (in order), then its unvisited hyperedges' members in each dynamic and each static 2-mode
        /// layer. Returns true, with the meeting node, at the first node the other side has also reached.
        /// </summary>
        private static bool Expand(Side side, Side other, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, out uint meeting, out int otherDist)
        {
            side.Level++;
            List<uint> next = [];
            EdgeTraversal traversal = side.Inbound ? EdgeTraversal.In : EdgeTraversal.Out;
            meeting = 0;
            otherDist = 0;
            foreach (uint u in side.Frontier)
            {
                foreach (var layer in oneModes)
                {
                    if (layer is LayerOneModeStatic packed)
                    {
                        if (!packed.NodeIdToIndexMapper.TryGetValue(u, out int li)) continue;
                        int[]? offsets;
                        uint[]? nbrs;
                        if (!packed.IsDirectional || !side.Inbound)
                        {
                            offsets = packed.CsrOffsets;
                            nbrs = packed.CsrNeighborNodeIds;
                        }
                        else
                        {
                            offsets = packed.CsrInOffsets;
                            nbrs = packed.CsrInNeighborNodeIds;
                            if (offsets == null || nbrs == null) continue;
                        }
                        for (int j = offsets[li]; j < offsets[li + 1]; j++)
                            if (side.Reach(nbrs[j], u, next, other, out otherDist)) { meeting = nbrs[j]; return true; }
                    }
                    else
                        foreach (uint v in layer.GetNodeAlters(u, traversal))
                            if (side.Reach(v, u, next, other, out otherDist)) { meeting = v; return true; }
                }

                for (int li = 0; li < twoModesDynamic.Count; li++)
                {
                    var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(u);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                        if (side.DynVisited[li].Add(he))
                            foreach (uint m in he.NodeIds)
                                if (side.Reach(m, u, next, other, out otherDist)) { meeting = m; return true; }
                }

                for (int li = 0; li < twoModesStatic.Count; li++)
                {
                    var layer = twoModesStatic[li];
                    if (!layer.TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                    for (int k = nStart; k < nEnd; k++)
                    {
                        int hIdx = layer.GetNodeHyperedgeIndex(k);
                        if (!side.StatVisited[li].Add(hIdx)) continue;
                        layer.GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                        for (int j = hStart; j < hEnd; j++)
                        {
                            uint m = layer.GetHyperedgeNodeAt(j);
                            if (side.Reach(m, u, next, other, out otherDist)) { meeting = m; return true; }
                        }
                    }
                }
            }
            side.Frontier = next;
            return false;
        }
    }
}