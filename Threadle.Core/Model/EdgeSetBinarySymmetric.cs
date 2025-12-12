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
    /// Describes an edgeset containing binary symmetric (non-directional) ties, i.e. implementing the
    /// IEdgesetBinary and IEdgesetSymmetric interfaces.
    /// </summary>
    public class EdgesetBinarySymmetric : IEdgesetBinary, IEdgesetSymmetric
    {
        #region Fields
        /// <summary>
        /// Collection of ties (i.e. the id of partner nodes).
        /// </summary>
        private readonly List<uint> _connections = [];
        #endregion


        #region Properties
        /// <summary>
        /// Returns the number of outbound edges in this edgeset, i.e. the
        /// total number of edges as they are symmetric.
        /// </summary>
        public uint NbrOutboundEdges { get => (uint)_connections.Count; }
        
        /// <summary>
        /// Returns the number of inbound edges in this edgeset, i.e. the
        /// total number of edges as they are symmetric.
        /// </summary>
        public uint NbrInboundEdges { get => (uint)_connections.Count; }
        
        /// <summary>
        /// Returns the number of edges in this edgeset. Only used when counting edges for view command,
        /// and needs to be the same for directional and symmetric layers.
        /// </summary>
        public uint NbrEdges { get => (uint)_connections.Count; }

        /// <summary>
        /// Returns a list of the outbound node ids in this edgeset.
        /// As edges are symmetric, it is the same set as the inbound.
        /// </summary>
        public List<uint> GetOutboundNodeIds { get => _connections; }
        
        /// <summary>
        /// Returns a list of the inbound node ids in this edgeset.
        /// As edges are symmetric, it is the same set as the outbound.
        /// </summary>
        public List<uint> GetInboundNodeIds { get => _connections; }

        /// <summary>
        /// Returns a list of all node ids in this edgeset.
        /// </summary>
        public List<uint> GetAllNodeIds { get => _connections; }
        #endregion


        #region Methods
        /// <summary>
        /// Adds an (inbound) edge to this edgeset. If the BlockMultiedges setting is active,
        /// it is first checked if this edge exists: if so, a warning is returned.
        /// </summary>
        /// <param name="partnerNodeId">The id of the partner node.</param>
        /// <param name="value">The value of the edge (has no effect here).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddInboundEdge(uint partnerNodeId, float value = 1)
        {
            return AddEdge(partnerNodeId);
            //if (UserSettings.BlockMultiedges && _connections.Contains(partnerNodeId))
            //    return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)");
            //_connections.Add(partnerNodeId);
            //return OperationResult.Ok();
        }

        /// <summary>
        /// Adds an (outbound) edge to this edgeset. If the BlockMultiedges setting is active,
        /// it is first checked if this edge exists: if so, a warning is returned.
        /// </summary>
        /// <param name="partnerNodeId">The id of the partner node.</param>
        /// <param name="value">The value of the edge (has no effect here).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddOutboundEdge(uint partnerNodeId, float value = 1)
        {
            return AddEdge(partnerNodeId);
            //if (UserSettings.BlockMultiedges && _connections.Contains(partnerNodeId))
            //    return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)");
            //_connections.Add(partnerNodeId);                
            //return OperationResult.Ok();
        }

        /// <summary>
        /// Private helper method for adding an edge, only used by the in- and outbound edge adder methods.
        /// </summary>
        /// <param name="partnerNodeId">The id of the partner node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        private OperationResult AddEdge(uint partnerNodeId)
        {
            if (UserSettings.BlockMultiedges && _connections.Contains(partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)");
            _connections.Add(partnerNodeId);
            return OperationResult.Ok();
        }

        /// <summary>
        /// Removes an (inbound) edge from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveInboundEdge(uint partnerNodeId)
        {
            return RemoveEdge(partnerNodeId);
            //if (_connections.Remove(partnerNodeId))
            //    return OperationResult.Ok();
            //return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        /// <summary>
        /// Removes an (outbound) edge from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveOutboundEdge(uint partnerNodeId)
        {
            return RemoveEdge(partnerNodeId);
            //if (_connections.Remove(partnerNodeId))
            //    return OperationResult.Ok();
            //return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        /// <summary>
        /// Private helper method for removing an edge, only used by the in- and outbound edge remover methods.
        /// </summary>
        /// <param name="partnerNodeId">The id of the partner node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult RemoveEdge(uint partnerNodeId)
        {
            if (_connections.Remove(partnerNodeId))
                return OperationResult.Ok();
            return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        /// <summary>
        /// Removes all edges in this edgeset with the specified nodeId.
        /// Used for cleaning up edgesets after a node has been removed from a nodeset.
        /// </summary>
        /// <param name="nodeId">The node id that should be searched for and eradicated.</param>
        public void RemoveNodeEdgesInEdgeset(uint nodeId)
        {
            _connections.Remove(nodeId);
        }

        /// <summary>
        /// Returns 1 if the edgeset contains an edge with the specified partner node.
        /// Returns 0 (zero) if no such edge exists.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>1 if the (binary) edge exists, otherwise 0.</returns>
        public float GetOutboundPartnerEdgeValue(uint partnerNodeId)
        {
            return (_connections.Contains(partnerNodeId)) ? 1 : 0;
        }

        /// <summary>
        /// Checks if there is an edge with the specified node.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>Returns true if the edge exists, false otherwise.</returns>
        public bool CheckOutboundPartnerEdgeExists(uint partnerNodeId)
        {
            return _connections.Contains(partnerNodeId);
        }

        /// <summary>
        /// This method produces a nodelist2-style string with alters, intended for save files.
        /// As symmetric edges should only be stored once in this format, I thus have to pass along
        /// the node id that 'owns' this edgeset, where I only store edges when the id of the source node
        /// is less than the id of the target node.
        /// This method is thus not useful for getting Alter ids, but only a helper for saving networks to file.
        /// </summary>
        /// <param name="egoNodeId">Reference node id</param>
        /// <returns>A tab-separated string with node ids.</returns>
        public string GetNodelistAlterString(uint egoNodeId)
        {
            string ret = "";
            foreach (uint alterNodeId in _connections)
                if (alterNodeId > egoNodeId)
                    ret += "\t" + alterNodeId;
            return ret;
        }

        public List<uint> GetNodelistAlterUints(uint nodeId)
        {
            List<uint> ret = [];
            foreach (uint alterNodeId in _connections)
            {
                if (alterNodeId > nodeId)
                    ret.Add(alterNodeId);                
            }
            return ret;
        }


        /// <summary>
        /// Returns an array of node ids in the edgeset, i.e. the
        /// set of alters. For symmetric data, the edge traversal
        /// does not matter.
        /// </summary>
        /// <param name="edgeTraversal"><see cref="EdgeTraversal"/>Declares whether alters should be inbound, outbound, or both - which is moot for symmetric edges.</param>
        /// <returns>An array of node ids.</returns>
        public uint[] GetAlterIds(EdgeTraversal edgeTraversal)
        {
            return [.. _connections];
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
            var edgeset = new EdgesetBinarySymmetric();
            foreach (uint nodeId in _connections)
                if (allowedNodes.Contains(nodeId))
                    edgeset._connections.Add(nodeId);
            return edgeset;
        }
        #endregion
    }
}
