namespace Threadle.Core.Model
{
    public interface ILayerTwoMode : ILayer
    {
        #region Properties
        uint NbrHyperedges { get; }
        #endregion


        #region Methods
        /// <summary>
        /// Returns true if a hyperedge with the specified name exists in this layer.
        /// </summary>
        bool ContainsHyperedge(string hyperName);

        /// <summary>
        /// Returns the node ids affiliated to the specified hyperedge, sorted by ascending node id.
        /// Returns an empty array if no hyperedge with that name exist, or if the hyperedge has no node ids
        /// affiliated to it.
        /// </summary>
        uint[] GetHyperedgeNodeIds(string hyperName);


        /// <summary>
        /// Returns the names of all hyperedges that the specified node is affiliated to.
        /// Returns an empty array if the node has no affiliations in this layer.
        /// </summary>
        string[] GetNodeHyperedgeNames(uint nodeId);


        /// <summary>
        /// Returns a paginated slice of all hyperedge names in the layer.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        string[] GetAllHyperedgeNames(int offset, int limit);
        #endregion
    }
}
