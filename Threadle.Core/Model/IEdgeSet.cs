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
    /// Interface for the classes that represents collections of edges that a node might have in a particular layer
    /// Whereas these edgesets might be different - binary or valued, directional or symmetric - they do share some
    /// common properties and are stored jointly in the layer under this interface.
    /// Whereas an IEdgeset relates to a particular node, that association is only done in the LayerOneMode class.
    /// </summary>
    public interface IEdgeset
    {
        #region Properties (shared)
        /// <summary>
        /// Returns the number of outbound edges
        /// </summary>
        uint NbrOutboundEdges { get; }

        /// <summary>
        /// Returns the number of inbound edges
        /// </summary>
        uint NbrInboundEdges { get; }

        /// <summary>
        /// Returns the number of edges in total
        /// </summary>
        uint NbrEdges { get; }

        /// <summary>
        /// Returns a list of node ids for all outbound edges in this edgeset.
        /// </summary>
        /// <returns>A List of node ids.</returns>
        List<uint> GetOutboundNodeIds { get; }

        /// <summary>
        /// Returns a list of node ids for all inbound edges in this edgeset.
        /// </summary>
        /// <returns>A List of node ids.</returns>
        List<uint> GetInboundNodeIds { get; }

        /// <summary>
        /// Returns a list of node ids for all inbound and outbound edges in this edgeset.
        /// </summary>
        /// <returns>A list of node ids.</returns>
        List<uint> GetAllNodeIds { get; }
        #endregion


        #region Methods (shared)
        /// <summary>
        /// Adds an inbound edge to this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node.</param>
        /// <param name="value">The value of the edge.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        OperationResult AddInboundEdge(uint partnerNodeId, float value);

        /// <summary>
        /// Adds an outbound edge to this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <param name="value">The value of the edge</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        OperationResult AddOutboundEdge(uint partnerNodeId, float value);

        /// <summary>
        /// Removes an inbound edge from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        OperationResult RemoveInboundEdge(uint partnerNodeId);

        /// <summary>
        /// Removes an outbound edge from this edgeset.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        OperationResult RemoveOutboundEdge(uint partnerNodeId);

        /// <summary>
        /// Removes all edges in the edgeset connected to the specified node id. Used for cleaning up 'lost edges',
        /// e.g. when a node has been removed.
        /// </summary>
        /// <param name="partnerNodeId">The id of the node (source or destination).</param>
        void RemoveNodeEdgesInEdgeset(uint partnerNodeId);

        /// <summary>
        /// Returns the value of the outbound edge to the specified node. Returns 0 (zero) if no such edge exists.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>The value of the edge (or zero if no such edge exists).</returns>
        float GetOutboundPartnerEdgeValue(uint partnerNodeId);

        /// <summary>
        /// Checks if there is an outbound edge to the specified node.
        /// </summary>
        /// <param name="partnerNodeId">The id of the destination node.</param>
        /// <returns>Returns true if the edge exists, false otherwise.</returns>
        bool CheckOutboundPartnerEdgeExists(uint partnerNodeId);

        /// <summary>
        /// This method produces a nodelist2-style string with alters, intended for save files.
        /// As symmetric edges should only be stored once in this format, I thus have to pass along
        /// the node id that 'owns' this edgeset, where I only store edges when the id of the source node
        /// is less than the id of the target node.
        /// This method is thus not useful for getting Alter ids, but rather a helper for saving networks to file.
        /// </summary>
        /// <param name="egoNodeId">Reference node id</param>
        /// <returns>A tab-separated string with node ids.</returns>
        string GetNodelistAlterString(uint egoNodeId);

        /// <summary>
        /// Returns an array of node ids in the edgeset, i.e. the set of alters. For directional data, this
        /// could either be outbound, inbound, or both, as dictated by the <paramref name="edgeTraversal"/> parameter.
        /// </summary>
        /// <param name="edgeTraversal"><see cref="EdgeTraversal"/>Declares whether alters should be inbound, outbound, or both.</param>
        /// <returns>An array of node ids.</returns>
        uint[] GetAlterIds(EdgeTraversal edgeTraversal);

        /// <summary>
        /// Removes all edges in the edgeset.
        /// </summary>
        void ClearEdges();

        /// <summary>
        /// Creates a copy of the Edgeset only including the edges mentioned in the provided HashSet.
        /// </summary>
        /// <param name="allowedNodes">A <see cref="HashSet"/> with allowed node ids.</param>
        /// <returns>A <see cref="IEdgeset"/> that is filtered.</returns>
        IEdgeset CreateFilteredCopy(HashSet<uint> allowedNodes);

        /// <summary>
        /// Adds an inbound edge to this edgeset, without any validation or similar.
        /// Only for bulk loading.
        /// </summary>
        /// <param name="partnerNodeId">The id of the source node</param>
        /// <param name="value">The value of the edge.</param>
        void _addInboundEdge(uint partnerNodeId, float value);

        /// <summary>
        /// Adds an outbound edge to this edgeset, without any validation or similar.
        /// Only for bulk loading.
        /// </summary>
        /// <param name="partnerNodeId"></param>
        /// <param name="value"></param>
        void _addOutboundEdge(uint partnerNodeId, float value);

        void _setCapacity(int capacity);
        #endregion
    }
}
