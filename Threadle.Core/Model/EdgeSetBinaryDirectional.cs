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
    /// Describes an edgeset containing binary directional ties, i.e. implementing the
    /// IEdgesetBinary and IEdgesetDirectional interfaces.
    /// </summary>
    public class EdgesetBinaryDirectional : IEdgesetBinary, IEdgesetDirectional
    {
        #region Fields
        /// <summary>
        /// Collection of outbound ties (i.e. the id of destination nodes).
        /// </summary>
        private List<uint> _outbound = [];

        /// <summary>
        /// Collection of inbound ties (i.e. the id of source nodes).
        /// </summary>
        private List<uint> _inbound = [];
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
        /// Returns a list of the outbound node ids in this edgeset.
        /// </summary>
        public List<uint> GetOutboundNodeIds { get => _outbound; }
        
        /// <summary>
        /// Returns a list of inbound node ids in this edgeset.
        /// </summary>
        public List<uint> GetInboundNodeIds { get => _inbound; }
        
        /// <summary>
        /// Returns a list of inbound and outbound node ids in this edgeset. Could thus contain duplicate node ids.
        /// </summary>
        public List<uint> GetAllNodeIds { get => [.. _outbound, .. _inbound]; }
        #endregion


        #region Methods
        /// <summary>
        /// Adds an inbound edge to this edgeset. If the BlockMultiedges setting is active,
        /// it is first checked if this edge exists: if so, a warning is returned.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node.</param>
        /// <param name="value">The value of the edge (has no effect here).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddInboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _inbound.Contains(partnerNodeId))
                return OperationResult.Fail("EdgeExists","Edge already exists (blocked)");
            _inbound.Add(partnerNodeId);
            return OperationResult.Ok();
        }

        public void _addInboundEdge(uint partnerNodeId, float value = 1)
        {
            _inbound.Add(partnerNodeId);
        }

        public void _addOutboundEdge(uint partnerNodeId, float value = 1)
        {
            _outbound.Add(partnerNodeId);
        }

        public void _deduplicate()
        {
            Misc.DeduplicateUintList(_outbound);
            Misc.DeduplicateUintList(_inbound);

        }

        /// <summary>
        /// Adds an outbound edge to this edgeset. If the BlockMultiedges setting is active,
        /// it is first checked if this edge exists: if so, a warning is returned.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <param name="value">The value of the edge (has no effect here).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddOutboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _outbound.Contains(partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)"); ;
            _outbound.Add(partnerNodeId);
            return OperationResult.Ok();
        }

        /// <summary>
        /// Removes an inbound edge from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveInboundEdge(uint nodeid)
        {
            if (_inbound.Remove(nodeid))
                return OperationResult.Ok();
            return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        /// <summary>
        /// Removes an outbound edge from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveOutboundEdge(uint nodeid)
        {
            if (_outbound.Remove(nodeid))
                return OperationResult.Ok();
            return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        /// <summary>
        /// Removes all inbound and outbound edges in this edgeset with the specified nodeId.
        /// Used for cleaning up edgesets after a node has been removed from a nodeset.
        /// </summary>
        /// <param name="nodeId">The node id that should be searched for and eradicated.</param>
        public void RemoveNodeEdgesInEdgeset(uint nodeId)
        {
            _inbound.Remove(nodeId);
            _outbound.Remove(nodeId);
        }

        /// <summary>
        /// Returns 1 if the edgeset contains an outbound edge to the specified destination node.
        /// Returns 0 (zero) if no such edge exists.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>1 if the (binary) edge exists, otherwise 0.</returns>
        public float GetOutboundPartnerEdgeValue(uint partnerNodeId)
        {
            return (_outbound.Contains(partnerNodeId)) ? 1 : 0;
        }

        /// <summary>
        /// Checks if there is an outbound edge to the specified node.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>Returns true if the edge exists, false otherwise.</returns>
        public bool CheckOutboundPartnerEdgeExists(uint partnerNodeId)
        {
            return _outbound.Contains(partnerNodeId);
        }

        /// <summary>
        /// This method produces a nodelist2-style string with alters, intended for save files.
        /// As directional edges are always stored as outgoing, where incoming edges can be seen as duplicates,
        /// there is only a need to store partner ids for outgoing edges.
        /// The egoNodeId, i.e. the owner of this edgeset, is passed along,
        /// but not necessary for directional edges.
        /// This method is thus not useful for getting Alter ids, but only a helper for saving networks to file.
        /// </summary>
        /// <param name="egoNodeId">Reference node id</param>
        /// <returns>A tab-separated string with node ids.</returns>
        public string GetNodelistAlterString(uint egoNodeId)
        {
            string ret = "";
            foreach (uint alterNodeId in _outbound)
                ret += "\t" + alterNodeId;
            return ret;
        }

        public List<uint> GetNodelistAlterUints(uint nodeId)
        {
            return GetOutboundNodeIds;
        }

        /// <summary>
        /// Returns an array of node ids in the edgeset, i.e. the set of alters. For directional data, this
        /// could either be outbound, inbound, or both, as dictated by the <paramref name="edgeTraversal"/> parameter.
        /// </summary>
        /// <param name="edgeTraversal"><see cref="EdgeTraversal"/>Declares whether alters should be inbound, outbound, or both.</param>
        /// <returns>An array of node ids.</returns>
        public uint[] GetAlterIds(EdgeTraversal edgeTraversal)
        {
            switch (edgeTraversal)
            {
                case EdgeTraversal.Out:
                    return _outbound.Count > 0 ? [.. _outbound] : [];
                case EdgeTraversal.In:
                    return _inbound.Count > 0 ? [.. _inbound] : [];
                case EdgeTraversal.Both:
                    if (_outbound.Count == 0) return _inbound.Count > 0 ? [.. _inbound] : [];
                    if (_inbound.Count == 0) return [.. _outbound];
                    var union = new HashSet<uint>(_outbound.Count + _inbound.Count);
                    foreach (var id in _outbound) union.Add(id);
                    foreach (var id in _inbound) union.Add(id);
                    return union.Count > 0 ? [.. union] : [];
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

        /// <summary>
        /// Creates a copy of the Edgeset only including the edges mentioned in the provided HashSet.
        /// </summary>
        /// <param name="allowedNodes">A <see cref="HashSet"/> with allowed node ids.</param>
        /// <returns>A <see cref="IEdgeset"/> that is filtered.</returns>
        public IEdgeset CreateFilteredCopy(HashSet<uint> allowedNodes)
        {
            var edgeset = new EdgesetBinaryDirectional();
            foreach (uint nodeId in _inbound)
                if (allowedNodes.Contains(nodeId))
                    edgeset._inbound.Add(nodeId);
            foreach (uint nodeId in _outbound)
                if (allowedNodes.Contains(nodeId))
                    edgeset._outbound.Add(nodeId);
            return edgeset;
        }

        public void _setCapacity(int capacity)
        {
            _outbound = new(capacity);
            //if (!UserSettings.OnlyOutboundEdges)
            _inbound = new(capacity);
        }

        #endregion
    }
}
