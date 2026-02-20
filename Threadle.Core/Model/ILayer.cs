using Threadle.Core.Model.Enums;

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

        /// <summary>
        /// Returns a string with metadata info about the layer
        /// </summary>
        string GetLayerInfo { get; }
        #endregion


        #region Methods
        /// <summary>
        /// Returns a HashSet of all unique node ids mentioned in the Layer
        /// </summary>
        /// <returns>A HashSet of node ids.</returns>
        HashSet<uint> GetMentionedNodeIds();

        /// <summary>
        /// Returns the value of a potential edge (or would-be projected edge) between two nodes. If no such edge
        /// exists, returns zero. For 1-mode layers, returns 1 for binary layers and the edge
        /// value for valued layers. For 2-mode layers, returns the number of affiliations (hyperedges)
        /// that the two nodes share.
        /// </summary>
        /// <param name="node1Id">The first node id.</param>
        /// <param name="node2Id">The second node id.</param>
        /// <returns>The value of the possible edge between the two nodes.</returns>
        float GetEdgeValue(uint node1Id, uint node2Id);

        /// <summary>
        /// Checks if an edge (or would-be projected edge) exists between the two nodes. For 2-mode layers, returns true if the two
        /// nodes share at least one affiliation (hyperedge). For 1-mode layers, returns true if the edge value is not zero.
        /// </summary>
        /// <param name="node1Id">The first node id.</param>
        /// <param name="node2Id">The second node id.</param>
        /// <returns>Returns true if there is a node, false otherwise.</returns>
        bool CheckEdgeExists(uint node1Id, uint node2Id);

        /// <summary>
        /// Returns an array of node ids for the alters for a specified ego node. For 1-mode layers, the alters are the nodes that are connected to the ego node by an edge (either in- or outbound, depending on the specified EdgeTraversal value). For 2-mode layers, the alters are the nodes that share at least one affiliation (hyperedge) with the ego node. Note that for 2-mode layers, the edgeTraversal parameter is not applicable and will be ignored.
        /// </summary>
        /// <param name="nodeId">The ego node id.</param>
        /// <param name="edgeTraversal">An <see cref="EdgeTraversal"/> value specifying whether in- or outbound edges should be included (or both). Only applicable
        /// for 1-mode layers.</param>
        /// <returns></returns>
        uint[] GetNodeAlters(uint nodeId, EdgeTraversal edgeTraversal);

        /// <summary>
        /// Removes all edges for a particular node id.
        /// </summary>
        /// <param name="nodeId"></param>
        void RemoveNodeEdges(uint nodeId);

        /// <summary>
        /// Removes all edges in the layer.
        /// </summary>
        void ClearLayer();

        /// <summary>
        /// Create a copy of this ILayer with those edges that refer to the nodes in the provided nodeset.
        /// </summary>
        /// <param name="nodeset">The <see cref="Nodeset"/> to filter on (only include edges with these node ids).</param>
        /// <returns>A copy of the current <see cref="ILayer"/> object with relevant edges.</returns>
        ILayer CreateFilteredCopy(Nodeset nodeset);

        /// <summary>
        /// Returns a list with the first n edges in the Layer. Used by the preview() CLI command.
        /// </summary>
        /// <param name="n">Number of edges to return (defaults to 10)</param>
        /// <returns>A list of strings</returns>
        List<string> GetNFirstEdges(int n = 10);
        #endregion
    }
}
