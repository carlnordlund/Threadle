namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes an affiliation, e.g. like a specific school class, a specific workplace or similar
    /// which is then an Hyperedge: a collection of Nodes that are connected.
    /// Each such affiliation should have a label (e.g. arbplatsId), but that is stored
    /// in LayerTwomode.AllHyperEdges dictionary, not in the actual Hyperedge object.
    /// </summary>
    public class Hyperedge
    {
        #region Fields
        /// <summary>
        /// List of node id that the Hyperedge connects.
        /// </summary>
        private List<uint> _nodeIds = [];

        /// <summary>
        /// Name of hyperedge (same as in AllHyperedges in LayerTwoMode)
        /// </summary>
        private string _name = string.Empty;
        #endregion


        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="Hyperedge"/> class,
        /// setting its connected endpoints to the provided array of node ids.
        /// </summary>
        /// <param name="nodeIds">An array of node ids that the Hyperedge connects.</param>
        public Hyperedge(uint[] nodeIds)
        {
            _nodeIds = [.. nodeIds];
        }

        /// <summary>
        /// Initializes a new instance of the Hyperedge class with the specified name.
        /// </summary>
        /// <param name="name">The name of the hyperedge. Cannot be null or empty.</param>
        public Hyperedge(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Initializes a new instance of the Hyperedge class with the specified name and associated node IDs.
        /// </summary>
        /// <remarks>Each node ID in the array must correspond to an existing node in the graph. Ensure
        /// that the provided node IDs are valid to avoid inconsistent graph state.</remarks>
        /// <param name="name">The name that uniquely identifies the hyperedge within the graph.</param>
        /// <param name="nodeIds">An array of unsigned integers representing the IDs of the nodes connected by this hyperedge. The array must
        /// not be null or empty.</param>
        public Hyperedge(string name, uint[] nodeIds)
        {
            _name = name;
            _nodeIds = [.. nodeIds];
        }
        #endregion


        #region Properties
        /// <summary>
        /// The list of node ids that the hyperedge connects.
        /// </summary>
        public IReadOnlyList<uint> NodeIds => _nodeIds;

        /// <summary>
        /// Returns the number of nodes connected by the hyperedge.
        /// </summary>
        public int NbrNodes => _nodeIds.Count;

        /// <summary>
        /// Returns the name of the hyperedge.
        /// </summary>
        public string Name => _name;
        #endregion


        #region Methods
        /// <summary>
        /// Adds the specified node to the hyperedge.
        /// </summary>
        /// <param name="nodeId">The node id to add.</param>
        internal void AddNode(uint nodeId)
        {
            _nodeIds.Add(nodeId);
        }

        /// <summary>
        /// Removes the node from the hyperedge.
        /// </summary>
        /// <param name="nodeId">The node id to remove.</param>
        /// <returns>true if the node was successfully removed; otherwise, false.</returns>
        internal bool RemoveNode(uint nodeId)
        {
            return _nodeIds.Remove(nodeId);
        }

        /// <summary>
        /// Disconnects the hyperedge from all node ids.
        /// </summary>
        internal void Clear()
        {
            _nodeIds.Clear();
        }

        /// <summary>
        /// Sets the node ids that the hyperedge is connected to (replacing any that it might
        /// be connected to prior). Assumes that nodeIds does not contain duplicates
        /// </summary>
        /// <param name="nodeIds">List of node ids that the hyperedge should connect.</param>
        internal void SetNodeIds(List<uint> nodeIds)
        {
            _nodeIds = nodeIds;
        }
        #endregion
    }
}
