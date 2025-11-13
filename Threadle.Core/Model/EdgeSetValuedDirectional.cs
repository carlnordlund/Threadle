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
    /// Describes an edgeset containing valued directional ties, i.e. implementing the
    /// IEdgesetValued and IEdgesetDirectional interfaces.
    /// </summary>
    public class EdgesetValuedDirectional : IEdgesetValued, IEdgesetDirectional
    {
        #region Fields
        /// <summary>
        /// Collection of outbound ties (i.e. the Connection structs of destination nodes).
        /// </summary>
        private readonly List<Connection> _outbound = [];

        /// <summary>
        /// Collection of inbound ties (i.e. the Connection structs of source nodes).
        /// </summary>
        private readonly List<Connection> _inbound = [];
        #endregion


        #region Properties
        /// <summary>
        /// Returns the number of outbound edges in this edgeset.
        /// </summary>
        public uint NbrOutboundEdges { get => (uint)_outbound.Count; }

        /// <summary>
        /// Returns the number of inbound edges in this edgeset.
        /// </summary>
        public uint NbrInboundEdges { get => (uint)_inbound.Count; }

        /// <summary>
        /// Returns the number of edges in this edgeset. Only used when counting edges for view command,
        /// and needs to be the same for directional and symmetric layers.
        /// </summary>
        public uint NbrEdges { get => (uint)_outbound.Count; }

        /// <summary>
        /// Returns a list of the outbound Connection structs in this edgeset.
        /// </summary>
        public List<Connection> GetOutboundConnections => _outbound;

        /// <summary>
        /// Returns a list of the inbound Connection structs in this edgeset.
        /// </summary>
        public List<Connection> GetInboundConnections => _inbound;

        /// <summary>
        /// Returns a list of the outbound node ids in this edgeset.
        /// </summary>
        public List<uint> GetOutboundNodeIds { get => [.. _outbound.Select(s => s.partnerNodeId)]; }

        /// <summary>
        /// Returns a list of the inbound node ids in this edgeset.
        /// </summary>
        public List<uint> GetInboundNodeIds { get => [.. _inbound.Select(s => s.partnerNodeId)]; }

        /// <summary>
        /// Returns a list of inbound and outbound node ids in this edgeset. Could thus contain duplicate node ids.
        /// </summary>
        public List<uint> GetAllNodeIds { get => [.. GetOutboundNodeIds.Concat(GetInboundNodeIds)]; }
        #endregion


        #region Methods
        /// <summary>
        /// Adds an inbound edge (Connection struct) to this edgeset. If the BlockMultiedges setting is active,
        /// it is first checked if this edge exists: if so, a warning is returned.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node.</param>
        /// <param name="value">The value of the edge.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddInboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _inbound.Any(s => s.partnerNodeId == partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)");
            _inbound.Add(new Connection(partnerNodeId, value));
            return OperationResult.Ok();
        }

        /// <summary>
        /// Adds an outbound edge (Connection struct) to this edgeset. If the BlockMultiedges setting is active,
        /// it is first checked if this edge exists: if so, a warning is returned.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <param name="value">The value of the edge.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddOutboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _outbound.Any(s => s.partnerNodeId == partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)");
            _outbound.Add(new Connection(partnerNodeId, value));
            return OperationResult.Ok();
        }

        /// <summary>
        /// Removes an inbound edge (i.e. Connection struct) from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveInboundEdge(uint partnerNodeId)
        {
            int index = _inbound.FindIndex(c => c.partnerNodeId == partnerNodeId);
            if (index >= 0)
            {
                _inbound.RemoveAt(index);
                return OperationResult.Ok();
            }
            return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        /// <summary>
        /// Removes an outbound edge (i.e. Connection struct) from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveOutboundEdge(uint partnerNodeId)
        {
            int index = _outbound.FindIndex(c => c.partnerNodeId == partnerNodeId);
            if (index >= 0)
            {
                _outbound.RemoveAt(index);
                return OperationResult.Ok();
            }
            return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        /// <summary>
        /// Removes all inbound and outbound edges (Connection structs) in this edgeset with the specified nodeId.
        /// Used for cleaning up edgesets after a node has been removed from a nodeset.
        /// </summary>
        /// <param name="nodeId">The node id that should be searched for and eradicated.</param>
        public void RemoveNodeEdges(uint nodeId)
        {
            _outbound.RemoveAll(c => c.partnerNodeId == nodeId);
            _inbound.RemoveAll(c => c.partnerNodeId == nodeId);
        }

        /// <summary>
        /// Returns the edge value if the edgeset contains an outbound edge to the specified destination node.
        /// Returns 0 (zero) if no such edge exists, or if the edge value is set to zero.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>1 if the (binary) edge exists, otherwise 0.</returns>
        public float GetOutboundPartnerEdgeValue(uint partnerNodeId)
        {
            foreach (var connection in _outbound)
                if (connection.partnerNodeId == partnerNodeId)
                    return connection.value;
            return 0;
        }

        /// <summary>
        /// Checks if there is an outbound edge to the specified node.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>Returns true if the edge exists, false otherwise.</returns>
        public bool CheckOutboundPartnerEdgeExists(uint partnerNodeId)
        {
            foreach (var connection in _outbound)
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
            foreach (Connection connection in _outbound)
                ret += $"\t{connection.partnerNodeId};{connection.value}";
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
            switch (edgeTraversal)
            {
                case EdgeTraversal.Out:
                    if (_outbound.Count == 0) return [];
                    var outboundIds = new uint[_outbound.Count];
                    for (int i = 0; i < _outbound.Count; i++)
                        outboundIds[i] = _outbound[i].partnerNodeId;
                    return outboundIds;
                case EdgeTraversal.In:
                    if (_inbound.Count == 0) return [];
                    var inboundIds = new uint[_inbound.Count];
                    for (int i = 0; i < _inbound.Count; i++)
                        inboundIds[i] = _inbound[i].partnerNodeId;
                    return inboundIds;
                case EdgeTraversal.Both:
                    if (_outbound.Count == 0) return GetAlterIds(EdgeTraversal.In);
                    if (_inbound.Count == 0) return GetAlterIds(EdgeTraversal.Out);
                    var union = new HashSet<uint>(_outbound.Count + _inbound.Count);
                    for (int i = 0; i < _outbound.Count; i++)
                        union.Add(_outbound[i].partnerNodeId);
                    for (int i=0; i< _inbound.Count; i++)
                        union.Add(_inbound[i].partnerNodeId);
                    return union.Count>0 ? [.. union] : [];
                default:
                    return [];
            }
        }

        /// <summary>
        /// Removes all edges in the edgeset.
        /// </summary>
        public void ClearEdges()
        {
            _outbound.Clear();
            _inbound.Clear();
        }
        #endregion
    }
}
