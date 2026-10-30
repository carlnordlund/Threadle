using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Model
{
    public interface ILayerOneMode : ILayer
    {
        #region Properties
        EdgeDirectionality Directionality { get; }
        EdgeType EdgeValueType { get; }
        bool IsSymmetric { get; }
        bool IsDirectional { get; }
        bool IsBinary { get; }
        bool IsValued { get; }
        bool Selfties { get; }

        /// <summary>
        /// Returns the total number of edges in this 1-mode layer
        /// </summary>
        ulong NbrEdges { get; }
        #endregion


        #region Methods
        /// <summary>
        /// Returns the number of outbound edges for the specified node. For undirected layers, equals the degree.
        /// </summary>
        uint GetOutDegree(uint nodeId);

        /// <summary>
        /// Returns the number of inbound edges for the specified node. For undirected layers, equals the degree.
        /// </summary>
        uint GetInDegree(uint nodeId);

        /// <summary>
        /// Returns a paginated list of all edges in the layer, each edge described as a dictionary with
        /// keys "node1" and "node2" (and for valued layers also "value").
        /// For undirected layers, each edge is returned only once (lower node id first).
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        List<Dictionary<string, object>> GetAllEdges(int offset = 0, int limit = 1000);

        /// <summary>
        /// Iterates all ego nodes with their outbound alters and edge values.
        /// For undirected layers, each edge is yielded only once (from the lower node id).
        /// <paramref name="values"/> is empty for binary layers.
        /// </summary>
        IEnumerable<(uint egoId, ReadOnlyMemory<uint> alters, ReadOnlyMemory<float> values)> GetAllEgoData();

        /// <summary>
        /// Invokes <paramref name="visitAlter"/> once for each alter of <paramref name="nodeId"/>
        /// per <paramref name="edgeTraversal"/>, without ever allocating a collection to hold the
        /// results - unlike <see cref="GetNodeAlters"/>, which always returns a freshly allocated
        /// array (and, for <see cref="EdgeTraversal.Both"/> on a directed layer, builds and
        /// discards a deduplicating HashSet on every call). For <see cref="EdgeTraversal.Both"/> on
        /// a directed layer, an alter present as both an outbound and an inbound neighbor of
        /// <paramref name="nodeId"/> is visited twice (once per direction) - callers that cannot
        /// tolerate that (anything accumulating a side effect per visit rather than just marking a
        /// node reached, e.g. Brandes' sigma/predecessor bookkeeping) must dedupe themselves or use
        /// <see cref="GetNodeAlters"/> instead for that specific combination. Traversal algorithms
        /// that only mark reachability (plain BFS) are unaffected, since marking the same node
        /// reached twice is a no-op.
        /// </summary>
        void VisitAlters(uint nodeId, EdgeTraversal edgeTraversal, Action<uint> visitAlter);

        #endregion

    }
}
