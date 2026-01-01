using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes an edgeset containing valued symmetric ties, i.e. implementing the
    /// IEdgesetValued and IEdgesetSymmetric interfaces.
    /// </summary>
    public class EdgesetValuedSymmetric : IEdgesetValued, IEdgesetSymmetric
    {
        #region Fields
        /// <summary>
        /// Collection of ties (i.e. the Connection structs of partner nodes).
        /// </summary>
        private List<Connection> _connections = [];
        #endregion


        #region Properties
        /// <summary>
        /// Returns the number of outbound edges in this edgeset, i.e. the
        /// total number of Connection structs as they are symmetric.
        /// </summary>
        public uint NbrOutboundEdges { get => (uint)_connections.Count; }

        /// <summary>
        /// Returns the number of inbound edges in this edgeset, i.e. the
        /// total number of Connection structs as they are symmetric.
        /// </summary>
        public uint NbrInboundEdges { get => (uint)_connections.Count; }

        /// <summary>
        /// Returns the number of edges in this edgeset. Only used when counting edges for view command,
        /// and needs to be the same for directional and symmetric layers.
        /// </summary>
        public uint NbrEdges { get => (uint)_connections.Count; }

        /// <summary>
        /// Returns a list of the outbound Connection structs in this edgeset, i.e. all
        /// Connection structs as they are symmetric.
        /// </summary>
        public IReadOnlyList<Connection> GetOutboundConnections => _connections;

        /// <summary>
        /// Returns a list of the inbound Connection structs in this edgeset, i.e. all
        /// Connection structs as they are symmetric.
        /// </summary>
        public IReadOnlyList<Connection> GetInboundConnections => _connections;

        /// <summary>
        /// Returns a list of the outbound node ids in this edgeset.
        /// As edges are symmetric, it is the same set as the inbound.
        /// </summary>
        public List<uint> GetOutboundNodeIds { get => [.. _connections.Select(s => s.partnerNodeId)]; }

        /// <summary>
        /// Returns a list of the inbound node ids in this edgeset.
        /// As edges are symmetric, it is the same set as the inbound.
        /// </summary>
        public List<uint> GetInboundNodeIds { get => [.. _connections.Select(s => s.partnerNodeId)]; }

        /// <summary>
        /// Returns a list of all node ids in this edgeset.
        /// </summary>
        public List<uint> GetAllNodeIds { get => [.. _connections.Select(s => s.partnerNodeId)]; }
        #endregion


        #region Methods
        /// <summary>
        /// Adds an (inbound) edge (Connection struct) to this edgeset with the specified value. If the BlockMultiedges setting is active,
        /// it is first checked if this edge exists: if so, a warning is returned.
        /// </summary>
        /// <param name="partnerNodeId">The id of the partner node.</param>
        /// <param name="value">The value of the edge (defaults to 1).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddInboundEdge(uint partnerNodeId, float value = 1)
        {
            return AddEdge(partnerNodeId, value);
        }

        /// <summary>
        /// Adds an (outbound) edge (Connection struct) to this edgeset with the specified value. If the BlockMultiedges setting is active,
        /// it is first checked if this edge exists: if so, a warning is returned.
        /// </summary>
        /// <param name="partnerNodeId">The id of the partner node.</param>
        /// <param name="value">The value of the edge (defaults to 1).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddOutboundEdge(uint partnerNodeId, float value = 1)
        {
            return AddEdge(partnerNodeId, value);
        }

        /// <summary>
        /// Private helper method for adding an edge, only used by the in- and outbound edge adder methods.
        /// </summary>
        /// <param name="partnerNodeId">The id of the partner node.</param>
        /// <param name="value">The value of the edge.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        private OperationResult AddEdge(uint partnerNodeId, float value)
        {
            if (UserSettings.BlockMultiedges && _connections.Any(s => s.partnerNodeId == partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)"); ;
            _connections.Add(new Connection(partnerNodeId, value));
            return OperationResult.Ok();
        }

        public void _addInboundEdge(uint partnerNodeId, float value = 1)
        {
            _connections.Add(new Connection(partnerNodeId, value));
        }

        public void _addOutboundEdge(uint partnerNodeId, float value = 1)
        {
            _connections.Add(new Connection(partnerNodeId, value));
        }

        public void _deduplicate()
        {
            Misc.DeduplicateConnectionList(_connections);
        }

        /// <summary>
        /// Removes an (inbound) edge from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveInboundEdge(uint partnerNodeId)
        {
            return RemoveEdge(partnerNodeId);
        }

        /// <summary>
        /// Removes an (outbound) edge from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveOutboundEdge(uint partnerNodeId)
        {
            return RemoveEdge(partnerNodeId);
        }

        /// <summary>
        /// Private helper method for removing an edge, only used by the in- and outbound edge remover methods.
        /// </summary>
        /// <param name="partnerNodeId">The id of the partner node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        private OperationResult RemoveEdge(uint partnerNodeId)
        {
            int index = _connections.FindIndex(c => c.partnerNodeId == partnerNodeId);
            if (index >= 0)
            {
                _connections.RemoveAt(index);
                return OperationResult.Ok();
            }
            return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        /// <summary>
        /// Removes all edges in this edgeset with the specified nodeId.
        /// Used for cleaning up edgesets after a node has been removed from a nodeset.
        /// </summary>
        /// <param name="nodeId">The node id that should be searched for and eradicated.</param>
        public void RemoveNodeEdgesInEdgeset(uint nodeId)
        {
            _connections.RemoveAll(c => c.partnerNodeId == nodeId);
        }

        /// <summary>
        /// Returns the value of the edge if the edgeset contains an edge with the specified partner node.
        /// Returns 0 (zero) if no such edge exists, or if the edge value is zero.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>1 if the (binary) edge exists, otherwise 0.</returns>
        public float GetOutboundPartnerEdgeValue(uint partnerNodeId)
        {
            foreach (var connection in _connections)
                if (connection.partnerNodeId == partnerNodeId)
                    return connection.value;
            return 0;
        }

        /// <summary>
        /// Checks if there is an edge with the specified node.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>Returns true if the edge exists, false otherwise.</returns>
        public bool CheckOutboundPartnerEdgeExists(uint partnerNodeId)
        {
            foreach (var connection in _connections)
                if (connection.partnerNodeId == partnerNodeId)
                    return true;
            return false;
        }

        /// <summary>
        /// This method produces a nodelist2-style string with alters and edge values, intended for save files.
        /// As directional edges are always stored as outgoing, where incoming edges can be seen as duplicates,
        /// there is only a need to store partner ids for outgoing edges.
        /// The egoNodeId, i.e. the owner of this edgeset, is passed along,
        /// but not necessary for directional edges.
        /// This method is thus not useful for getting Alter ids, but only a helper for saving networks to file.
        /// </summary>
        /// <param name="egoNodeId">Reference node id</param>
        /// <returns>A tab-separated string with node ids and edge values.</returns>
        public string GetNodelistAlterString(uint egoNodeId)
        {
            string ret = "";
            foreach (Connection connection in _connections)
                if (connection.partnerNodeId > egoNodeId)
                    ret += $"\t{connection.partnerNodeId};{connection.value}";
            return ret;
        }

        /// <summary>
        /// Gets a list of all partner Connection structs. For symmetric edgesets, edges should only be stored once
        /// so I pass along nodeId to make sure I only get Connection structs for partner ids that are larger than ego node id
        /// </summary>
        /// <param name="nodeId">The node id of ego</param>
        /// <returns>A List of Connection structs.</returns>
        public IReadOnlyList<Connection> GetNodelistAlterConnections(uint egoNodeId)
        {
            var ret = new List<Connection>();
            foreach (var connection in _connections)
                if (connection.partnerNodeId > egoNodeId)
                    ret.Add(connection);
            return ret;
        }


        /// <summary>
        /// Returns an array of node ids in the edgeset, i.e. the set of alters. For directional data, this
        /// could either be outbound, inbound, or both, as dictated by the <paramref name="edgeTraversal"/> parameter.
        /// </summary>
        /// <param name="edgeTraversal">An <see cref="EdgeTraversal"/> enum value whether alters should be inbound, outbound, or both.</param>
        /// <returns>An array of node ids.</returns>
        public uint[] GetAlterIds(EdgeTraversal edgeTraversal)
        {
            if (_connections.Count == 0)
                return [];
            uint[] ids = new uint[_connections.Count];
            for (int i = 0; i < _connections.Count; i++)
                ids[i] = _connections[i].partnerNodeId;
            return ids;
        }

        /// <summary>
        /// Removes all edges in the edgeset.
        /// </summary>
        public void ClearEdges()
        {
            _connections.Clear();
        }

        /// <summary>
        /// Creates a copy of the Edgeset only including the edges mentioned in the provided HashSet.
        /// </summary>
        /// <param name="allowedNodes">A <see cref="HashSet"/> with allowed node ids.</param>
        /// <returns>A <see cref="IEdgeset"/> that is filtered.</returns>
        public IEdgeset CreateFilteredCopy(HashSet<uint> allowedNodes)
        {
            var edgeset = new EdgesetValuedSymmetric();
            foreach (Connection conn in _connections)
                if (allowedNodes.Contains(conn.partnerNodeId))
                    edgeset._connections.Add(conn);
            return edgeset;
        }

        public void _setCapacity(int capacity)
        {
            _connections = new(capacity);
        }
        #endregion
    }
}
