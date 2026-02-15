namespace Threadle.Core.Model
{
    /// <summary>
    /// Interface for the two edgeset classes that contain binary edges.
    /// </summary>
    public interface IEdgesetBinary : IEdgeset
    {
        /// <summary>
        /// Gets a list of all partner node ids. For symmetric edgesets, edges should only be stored once
        /// so I pass along egoNodeId to make sure I only get partner node ids that are larger than ego node id
        /// </summary>
        /// <param name="egoNodeId">The node id of ego</param>
        /// <returns>A List of partner node ids.</returns>
        List<uint> GetNodelistAlterUints(uint egoNodeId);
    }
}
