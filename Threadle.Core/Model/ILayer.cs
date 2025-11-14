using Threadle.Core.Model.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Specifies common features of relational layers - interface for LayerOneMode and LayerTwoMode
    /// </summary>
    public interface ILayer
    {
        #region Properties
        /// <summary>
        /// Returns the name of this layer.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Returns a dictionary (possibly nested) with information about the layer.
        /// </summary>
        Dictionary<string, object> GetMetadata { get; }
        #endregion


        #region Methods
        /// <summary>
        /// Returns a HashSet of all unique node ids mentioned in the Layer
        /// </summary>
        /// <returns>A HashSet of node ids.</returns>
        HashSet<uint> GetMentionedNodeIds();
        
        /// <summary>
        /// Returns the value of a potential edge between two nodes. If no edge
        /// exists, returns zero. For 1-mode layers, returns 1 for binary layers and the edge
        /// value for valued layers. For 2-mode layers, returns the number of affiliations (hyperedges)
        /// that the two nodes share.
        /// </summary>
        /// <param name="node1">The first node id.</param>
        /// <param name="node2">The second node id.</param>
        /// <returns>The value of the possible edge between the two nodes.</returns>
        float GetEdgeValue(uint node1, uint node2);

        /// <summary>
        /// Checks if an edge exists between the two nodes. For 2-mode layers, returns true if the two
        /// nodes share at leasts one affil
        /// </summary>
        /// <param name="node1">The first node id.</param>
        /// <param name="node2">The second node id.</param>
        /// <returns>Returns true if there is a node, false otherwise.</returns>
        bool CheckEdgeExists(uint node1, uint node2);
        
        /// <summary>
        /// Returns an array of node ids for the alters for a specified ego node.
        /// </summary>
        /// <param name="nodeId">The ego node id.</param>
        /// <param name="edgeTraversal">An <see cref="EdgeTraversal"/> value specifying whether in- or outbound edges should be included (or both).</param>
        /// <returns></returns>
        uint[] GetAlterIds(uint nodeId, EdgeTraversal edgeTraversal);

        /// <summary>
        /// Removes all edges for a particular node id.
        /// </summary>
        /// <param name="nodeId"></param>
        void RemoveNodeEdges(uint nodeId);

        /// <summary>
        /// Removes all edges in the layer.
        /// </summary>
        void ClearLayer();
        #endregion
    }
}
