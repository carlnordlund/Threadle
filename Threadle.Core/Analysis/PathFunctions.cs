using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

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