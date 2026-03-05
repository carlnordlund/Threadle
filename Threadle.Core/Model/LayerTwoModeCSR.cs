using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Model
{
    public class LayerTwoModeCSR : ILayer
    {
        #region Fields
        /// <summary>
        /// List of hyperedge names, ordered by ther indexes
        /// </summary>
        private string[] _hyperedgeNames = [];

        /// <summary>
        /// The offsets for hyperedge indexes for looking in _hyperedgeNodeIdsFlat, i.e. to quickly find the sets of node ids that a hyperedge has
        /// </summary>
        private int[] _offsetsHyperedges = [0];

        /// <summary>
        /// This is where I store consecutive series of node ids that each hyperedge has (long flat array)
        /// </summary>
        private uint[] _hyperedgeNodeIdsFlat = [];

        // Then, given a node id, quickly find its location in the nodeIdOffsets array:

        /// <summary>
        /// Dictionary to map node Ids to node index
        /// </summary>
        private Dictionary<uint, int> _nodeIdToIndexMapper = [];

        // this is the offset lookup for looking in _nodeIdHyperedges, i.e. to quickly find the hyperedge indexes that a node Id has
        /// <summary>
        /// The offsets for node indexes for looking in _nodeIdHyperedgesFlat, i.e. to quickly find the sets of hyperedge indexes that each node is affiliated to
        /// </summary>
        private int[] _offsetsNodeIds = [0];

        /// <summary>
        /// This is where I store consecutive series of hyperedge indexes that each node Id is affiliated to (long flat array)
        /// </summary>
        private int[] _nodeIdHyperedgesFlat = [];


        // So: a checkedge(node1id, node2id) would then be like this:
        // 1. Use _nodeIdToIndexMapper to find the index of the node: indexNode
        // 2. Go into _offsetsNodeIds: start = _offsetNodeIds[indexNode], end = _offsetNodeIds[indexNode+1]
        // 3. Get the set of hyperedgeIndices that this nodeId is connected to: indexHyper. Iterate through them
        // 4.   For each hyperedge: Go into _offsetHyperedges: get the start = _offsetHyperedges[indexHyper], end = _offsetHyperedges[indexHyper+1]
        // 5.   Check in _hyperedgeNodeIds if node2Id is there: if yes, return true

        // Getedge would be similar, but in step 5: instead of returning, just increase a value, continue etc. return that value.

        // So, no dictionary to get hyperedge content: the CLI command 'gethyperedgenodes(... hypername=[str])' would thus be slow and having to iterate through hyperedgeNames
        // to find the index, then from there get the node ids. Fairly rare function so probably okay. The CSR is basically snapshotting with focus on having
        // speedy getedge, checkedge, and getnodealters only

        #endregion



        #region Constructors
        private LayerTwoModeCSR(string name, string[] hyperedgeNames, int[] offsetsHyperedges, uint[] hyperedgesNodeIdsFlat, Dictionary<uint,int> nodeIdToIndexMapper, int[] offsetsNodeIds, int[] nodeIdHypedgesFlat)
        {
            Name = name;
            _hyperedgeNames= hyperedgeNames;
            _offsetsHyperedges= offsetsHyperedges;
            _hyperedgeNodeIdsFlat = hyperedgesNodeIdsFlat;
            _nodeIdToIndexMapper = nodeIdToIndexMapper;
            _offsetsNodeIds = offsetsNodeIds;
            _nodeIdHyperedgesFlat = nodeIdHypedgesFlat;
        }
        #endregion


        #region Properties
        /// <summary>
        /// The name of the layer.
        /// </summary>
        public string Name { get; set; } = "";
        #endregion


        #region Methods (public)
        /// <summary>
        /// Returns metadata about the layer (as a dictionary of objects).
        /// </summary>
        public Dictionary<string, object> GetMetadata => new()
        {
            ["Name"] = Name,
            ["Mode"] = 2,
            ["Static"] = false//,
            //["NbrHyperedges"] = NbrHyperedges
        };

        /// <summary>
        /// Returns a string with metadata info about the layer
        /// </summary>
        public string GetLayerInfo => $" {Name} [2-mode; Nbr hyperedges: ]";

        public bool IsStatic => true;
        #endregion


        #region Methods (public)
        public static LayerTwoModeCSR FromDynamic(LayerTwoMode source)
        {
            var hyperedgeNames = source.AllHyperEdges.Keys.ToArray();
            var hyperedgeNameToIndex = hyperedgeNames.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i);

            var hyperedgeNodeIdsList = new List<uint>();
            var offsetHyperedges = new int[hyperedgeNames.Length + 1];
            for (int i = 0; i < hyperedgeNames.Length; i++)
            {
                offsetHyperedges[i] = hyperedgeNodeIdsList.Count;
                var nodeIds = source.AllHyperEdges[hyperedgeNames[i]].NodeIds.OrderBy(x => x).ToArray();
                hyperedgeNodeIdsList.AddRange(nodeIds);
            }
            offsetHyperedges[hyperedgeNames.Length] = hyperedgeNodeIdsList.Count;

            var sortedNodeIds = source.HyperEdgeCollections.Keys.OrderBy(x => x).ToArray();
            var mapper = new Dictionary<uint, int>(sortedNodeIds.Length);
            var nodeIdHyperedgesList = new List<int>();
            var offsetNodeIds = new int[sortedNodeIds.Length + 1];

            for (int i = 0; i < sortedNodeIds.Length; i++)
            {
                uint nodeId = sortedNodeIds[i];
                mapper[nodeId] = i;
                offsetNodeIds[i] = nodeIdHyperedgesList.Count;
                foreach (var hyperedge in source.HyperEdgeCollections[nodeId].HyperEdges)
                    nodeIdHyperedgesList.Add(hyperedgeNameToIndex[hyperedge.Name]);
            }
            offsetNodeIds[sortedNodeIds.Length] = nodeIdHyperedgesList.Count;
            return new LayerTwoModeCSR(source.Name, hyperedgeNames, offsetHyperedges, hyperedgeNodeIdsList.ToArray(), mapper, offsetNodeIds, nodeIdHyperedgesList.ToArray());

        }





        /// <summary>
        /// Gets the edge value between node1Id and node2Id if this exists.
        /// As this is a 2-mode layer, this returns the number of shared Hyperedge objects that these two
        /// node ids have, as such reflecting the value that would emerge when a 2-mode network is projected
        /// to a 1-mode counterpart.
        /// </summary>
        /// <param name="node1Id">The first node id.</param>
        /// <param name="node2Id">The second node id.</param>
        /// <returns>The value of the projected edge, i.e. the number of shared affiliations they have.</returns>
        public float GetEdgeValue(uint node1Id, uint node2Id)
        {
            // First, check if this node is mapped on this layer: if not, it has no hyperedges here. Otherwise: get the index of this node
            if (!_nodeIdToIndexMapper.TryGetValue(node1Id, out int node1Index))
                return 0f;
            // Use the node index to get the start and end indices in the _nodeIdHyperedges (which stores the hyperedge indexes for each node)
            int start = _offsetsNodeIds[node1Index], end = _offsetsNodeIds[node1Index + 1];
            // Counter for the number of shared hyperedges
            int sharedHyperedges = 0;

            // Iterate through these hyperedge indices
            for (int k = start; k < end; k++)
            {
                // Get the index of each hyperedge
                int h = _nodeIdHyperedgesFlat[k];
                // Get the start and end indices in the _hyperedgeNodeIds (which stores the node Ids for each hyperedge)
                int hStart = _offsetsHyperedges[h];
                int hEnd = _offsetsHyperedges[h + 1];
                // Search through these parts of the _hyperedgeNodeIds to see if node2Id is there somewhere. If so, return true
                if (Array.BinarySearch(_hyperedgeNodeIdsFlat, hStart, hEnd - hStart, node2Id) >= 0)
                    sharedHyperedges++;
            }
            // Nope, iterated through all the hyperedges that node1Id belongs to, iterating through the node Ids for these hyperedges,
            // but couldn't find node2Id there anywhere
            return sharedHyperedges;
        }

        /// <summary>
        /// Checks if an edge exists between node1Id and node2Id.
        /// As this is a 2-mode layer, this checks if there is at least one Hyperedge where both exist.
        /// Goes through the set of Hyperedge objects of node1Id and checks if node2Id is in any of these.
        /// </summary>
        /// <param name="node1Id">The first node id.</param>
        /// <param name="node2Id">The second node id.</param>
        /// <returns>Returns true if the two nodes share at least one affiliation (hyperedge).</returns>
        public bool CheckEdgeExists(uint node1Id, uint node2Id)
        {
            // First, check if this node is mapped on this layer: if not, it has no hyperedges here. Otherwise: get the index of this node
            if (!_nodeIdToIndexMapper.TryGetValue(node1Id, out int node1Index))
                return false;
            // Use the node index to get the start and end indices in the _nodeIdHyperedges (which stores the hyperedge indexes for each node)
            int start = _offsetsNodeIds[node1Index], end = _offsetsNodeIds[node1Index + 1];
            // Iterate through these hyperedge indices
            for (int k = start; k < end; k++)
            {
                // Get the index of each hyperedge
                int h = _nodeIdHyperedgesFlat[k];
                // Get the start and end indices in the _hyperedgeNodeIds (which stores the node Ids for each hyperedge)
                int hStart = _offsetsHyperedges[h];
                int hEnd = _offsetsHyperedges[h + 1];
                // Search through these parts of the _hyperedgeNodeIds to see if node2Id is there somewhere. If so, return true
                if (Array.BinarySearch(_hyperedgeNodeIdsFlat, hStart, hEnd - hStart, node2Id) >= 0)
                    return true;
            }
            // Nope, iterated through all the hyperedges that node1Id belongs to, iterating through the node Ids for these hyperedges,
            // but couldn't find node2Id there anywhere
            return false;
        }

        /// <summary>
        /// Returns an array of node ids in the edgeset, i.e. the set of alters.
        /// As this is 2-mode data, this is the set of all unique node ids of the Hyperedge objects of a particular
        /// node id, minus the actual ego node id.
        /// </summary>
        /// <param name="edgeTraversal">As this is a 2-mode network, this is moot here.</param>
        /// <returns>An array of node ids.</returns>
        public uint[] GetNodeAlters(uint nodeId, EdgeTraversal edgeTraversal = EdgeTraversal.Both)
        {
            // First, check if this node is mapped on this layer: if not, it has no hyperedges here. Otherwise: get the index of this node
            if (!_nodeIdToIndexMapper.TryGetValue(nodeId, out int nodeIndex))
                return [];
            // Use the node index to get the start and end indices in the _nodeIdHyperedges (which stores the hyperedge indexes for each node)
            int start = _offsetsNodeIds[nodeIndex], end = _offsetsNodeIds[nodeIndex + 1];
            // Prepare Hashset for storing alters. HashSets are nice here as they keep only unique values.
            var alters = new HashSet<uint>();

            for (int k = start; k < end; k++)
            {
                int h = _nodeIdHyperedgesFlat[k];
                int hStart = _offsetsHyperedges[h];
                int hEnd = _offsetsHyperedges[h + 1];
                for (int j = hStart; j < hEnd; j++)
                {
                    uint alterId = _hyperedgeNodeIdsFlat[j];
                    if (alterId != nodeId)
                        alters.Add(alterId);
                }
            }
            return alters.ToArray();
        }

        /// <summary>
        /// Clears the layer, i.e. clears all Edgeset objects and removes these.
        /// </summary>
        public void ClearLayer()
        {
            _hyperedgeNames = [];
            _offsetsHyperedges = [0];
            _hyperedgeNodeIdsFlat = [];
            _nodeIdToIndexMapper.Clear();
            _offsetsNodeIds = [0];
            _nodeIdHyperedgesFlat = [];
        }

        /// <summary>
        /// Removes all Hyperedge references for a nodeId. Also goes through all existing Hyperedge objects
        /// and removes the references to this node ids. Used when a node is deleted and all edges must be
        /// remove.
        /// </summary>
        /// <param name="nodeId">The node id that is to be removed from Hyperedge references.</param>
        public void RemoveNodeEdges(uint nodeId)
        {
            var newOffsetHyperedges = new int[_hyperedgeNames.Length + 1];
            var newHyperedgeNodeIds = new List<uint>(_hyperedgeNodeIdsFlat.Length);

            for (int h = 0; h < _hyperedgeNames.Length; h++)
            {
                newOffsetHyperedges[h] = newHyperedgeNodeIds.Count;
                for (int j = _offsetsHyperedges[h]; j < _offsetsHyperedges[h + 1]; j++)
                    if (_hyperedgeNodeIdsFlat[j] != nodeId)
                        newHyperedgeNodeIds.Add(_hyperedgeNodeIdsFlat[j]);
            }

            var newMapper = new Dictionary<uint, int>(_nodeIdToIndexMapper.Count);
            var newOffsetNodeIds = new List<int> { 0 };
            var newNodeIdHyperedges = new List<int>(_nodeIdHyperedgesFlat.Length);

            foreach (uint egoId in _nodeIdToIndexMapper.Keys.OrderBy(id => _nodeIdToIndexMapper[id]))
            {
                if (egoId == nodeId)
                    continue;
                int i = _nodeIdToIndexMapper[egoId];
                newMapper[egoId] = newMapper.Count;
                for (int k = _offsetsNodeIds[i]; k < _offsetsNodeIds[i + 1]; k++)
                    newNodeIdHyperedges.Add(newNodeIdHyperedges.Count);
            }

            _offsetsHyperedges = newOffsetHyperedges;
            _hyperedgeNodeIdsFlat = newHyperedgeNodeIds.ToArray();
            _nodeIdToIndexMapper = newMapper;
            _offsetsNodeIds = newOffsetNodeIds.ToArray();
            _nodeIdHyperedgesFlat = newNodeIdHyperedges.ToArray();
        }

        /// <summary>
        /// Create a new filtered version of this layer that only includes affiliations with the nodes
        /// in the provided Nodeset
        /// </summary>
        /// <returns>A copy of the current <see cref="ILayer"/> object.</returns>
        public ILayer CreateFilteredCopy(Nodeset nodeset)
        {
            var newOffsetHyperedges = new int[_hyperedgeNames.Length + 1];
            var newHyperedgeNodeIds = new List<uint>(_hyperedgeNodeIdsFlat.Length);

            for (int h = 0; h < _hyperedgeNames.Length; h++)
            {
                newOffsetHyperedges[h] = newHyperedgeNodeIds.Count;
                for (int j = _offsetsHyperedges[h]; j < _offsetsHyperedges[h + 1]; j++)
                    if (!nodeset.Contains(_hyperedgeNodeIdsFlat[j]))
                        newHyperedgeNodeIds.Add(_hyperedgeNodeIdsFlat[j]);
            }

            var newMapper = new Dictionary<uint, int>(_nodeIdToIndexMapper.Count);
            var newOffsetNodeIds = new List<int> { 0 };
            var newNodeIdHyperedges = new List<int>(_nodeIdHyperedgesFlat.Length);

            foreach (uint egoId in _nodeIdToIndexMapper.Keys.OrderBy(id => _nodeIdToIndexMapper[id]))
            {
                if (!nodeset.Contains(egoId))
                    continue;
                int i = _nodeIdToIndexMapper[egoId];
                newMapper[egoId] = newMapper.Count;
                for (int k = _offsetsNodeIds[i]; k < _offsetsNodeIds[i + 1]; k++)
                    newNodeIdHyperedges.Add(newNodeIdHyperedges.Count);
            }

            return new LayerTwoModeCSR(Name + "_filtered", _hyperedgeNames, newOffsetHyperedges, newHyperedgeNodeIds.ToArray(), newMapper, newOffsetNodeIds.ToArray(), newNodeIdHyperedges.ToArray());
        }

        /// <summary>
        /// Returns a list of strings with the first n node-to-hyperedge affiliations Layer.
        /// Used by the preview() CLI command.
        /// </summary>
        /// <param name="n">Number of edges to return (defaults to 10)</param>
        /// <returns>A list of strings</returns>
        public List<string> GetNFirstEdges(int n = 10)
        {
            throw new NotImplementedException();
        }
        #endregion


    }
}
