using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Represents a relational layer of 2-mode relations. Implements ILayer.
    /// The 2-mode layer consists of a global collection of Hyperedge objects, and a dictionary
    /// of these Hyperedge objects by node id.
    /// </summary>
    public class LayerTwoMode : ILayer
    {
        #region Fields
        /// <summary>
        /// Dictionary of HyperedgeCollection objects by node id.
        /// </summary>
        private Dictionary<uint, HyperedgeCollection> _hyperedgeCollections = [];

        /// <summary>
        /// Dictionary of all Hyperedge objects, accessible by unique names for each Hyperedge.
        /// </summary>
        private Dictionary<string, Hyperedge> _allHyperedges = [];
        #endregion


        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="LayerTwoMode"/> class
        /// </summary>
        public LayerTwoMode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LayerTwoMode"/> class, setting its name
        /// </summary>
        /// <param name="name"></param>
        public LayerTwoMode(string name)
        {
            Name = name;
        }
        #endregion


        #region Properties
        /// <summary>
        /// The name of the layer.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Returns the number of Hyperedge objects in this layer.
        /// </summary>
        public uint NbrHyperedges { get => (uint)_allHyperedges.Count; }

        /// <summary>
        /// Returns the dictionary of Hyperedge objects by node id
        /// </summary>
        public Dictionary<uint, HyperedgeCollection> HyperEdgeCollections => _hyperedgeCollections;

        /// <summary>
        /// Returns the dictionary of all Hyperedge objects by their unique names
        /// </summary>
        public Dictionary<string, Hyperedge> AllHyperEdges => _allHyperedges;

        /// <summary>
        /// Returns metadata about the layer (as a dictionary of objects).
        /// </summary>
        public Dictionary<string, object> GetMetadata => new()
        {
            ["Name"] = Name,
            ["Mode"] = 2,
            ["NbrHyperedges"] = NbrHyperedges
        };

        /// <summary>
        /// Returns a string with metadata info about the layer
        /// </summary>
        public string GetLayerInfo => $" {Name} [2-mode; Nbr hyperedges: {NbrHyperedges}]";
        #endregion


        #region Methods (public)
        /// <summary>
        /// Removes all Hyperedge references for a nodeId. Also goes through all existing Hyperedge objects
        /// and removes the references to this node ids. Used when a node is deleted and all edges must be
        /// remove.
        /// </summary>
        /// <param name="nodeId">The node id that is to be removed from Hyperedge references.</param>
        public void RemoveNodeEdges(uint nodeId)
        {
            HyperEdgeCollections.Remove(nodeId);
            foreach (Hyperedge hyperedge in AllHyperEdges.Values)
                hyperedge.RemoveNode(nodeId);
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
            /// Check that both node ids are part of any hyperedges at all, and that these hyperedges have any node ids. If not, return 0.
            if (GetNonEmptyHyperedgeCollection(node1Id) is not HyperedgeCollection sourceCollection || GetNonEmptyHyperedgeCollection(node2Id) is not HyperedgeCollection targetCollection)
                return 0f;
            /// Go through the set of Hyperedge objects of node1Id and check how many of these have node2Id in their node id array. This is the value of the projected edge.
            return (sourceCollection.HyperEdges.Intersect(targetCollection.HyperEdges)).Count();
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
            // Check that both node ids are part of any hyperedges at all, and that these hyperedges have any node ids. If not, return false.
            if (GetNonEmptyHyperedgeCollection(node1Id) is not HyperedgeCollection hyperEdgeCollection || GetNonEmptyHyperedgeCollection(node2Id) is null)
                return false;
            // Go through the set of Hyperedge objects of node1Id and check if node2Id is in any of these. If so, return true. If not, return false.
            foreach (Hyperedge hyperedge in hyperEdgeCollection.HyperEdges)
                if (hyperedge.NodeIds.Contains(node2Id))
                    return true;
            // If we get here, there is no shared hyperedge, so return false.
            return false;
        }

        /// <summary>
        /// Determines whether a hyperedge with the specified name exists in the collection.
        /// </summary>
        /// <param name="hyperName">The name of the hyperedge.</param>
        /// <returns>true if a hyperedge with the specified name exists; otherwise, false.</returns>
        public bool CheckThatHyperedgeExists(string hyperName)
        {
            return AllHyperEdges.ContainsKey(hyperName);
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
            /// Note that the edgeTraversal parameter is only relevant in the implementation in LayerOneMode.
            /// Check that the node id is part of any hyperedges at all, and that these hyperedges have any node ids. If not, return an empty array.
            if (GetNonEmptyHyperedgeCollection(nodeId) is not HyperedgeCollection hyperEdgeCollection)
                return [];
            // Prepare a set of alters, i.e. the unique node ids of all Hyperedge objects of this node id, minus the actual ego node id.
            // As this is a HashSet, it will automatically filter out duplicates.
            HashSet<uint> alters = [];
            // Go through the Hyperedge objects of this node id and add their node ids to the set of alters.
            foreach (Hyperedge hyperEdge in hyperEdgeCollection.HyperEdges)
                alters.UnionWith(hyperEdge.NodeIds);
            // Remove the actual ego node id from the set of alters, which will have been added in the previous step.
            alters.Remove(nodeId);
            return [.. alters];
        }

        /// <summary>
        /// Returns a HashSet of all unique node ids mentioned in the Layer
        /// </summary>
        /// <returns>A HashSet of node ids.</returns>
        public HashSet<uint> GetMentionedNodeIds()
        {
            HashSet<uint> ids = [];
            foreach (Hyperedge hyperEdge in AllHyperEdges.Values)
            {
                ids.UnionWith(hyperEdge.NodeIds);
            }
            return ids;
        }

        /// <summary>
        /// Clears the layer, i.e. clears all Edgeset objects and removes these.
        /// </summary>
        public void ClearLayer()
        {
            HyperEdgeCollections.Clear();
            foreach (Hyperedge hyperedge in AllHyperEdges.Values)
                hyperedge.Clear();
            AllHyperEdges.Clear();
        }

        /// <summary>
        /// Create a new empty copy of this ILayer
        /// </summary>
        /// <returns>A copy of the current <see cref="ILayer"/> object.</returns>
        public ILayer CreateFilteredCopy(Nodeset nodeset)
        {
            HashSet<uint> allowedNodeIds = [.. nodeset.NodeIdArray];
            LayerTwoMode layerCopy = CreateEmptyCopy();
            foreach (var (name, hyperedge) in AllHyperEdges)
            {
                Hyperedge hyperedge_filtered = new(hyperedge.NodeIds.Intersect(allowedNodeIds).ToArray());
                if (hyperedge_filtered.NbrNodes == 0)
                    continue;
                layerCopy.AllHyperEdges.Add(name, hyperedge_filtered);
                foreach (uint nodeId in hyperedge_filtered.NodeIds)
                    layerCopy.AddHyperEdgeToNode(nodeId, hyperedge_filtered);
            }
            return layerCopy;
        }

        /// <summary>
        /// Returns a list of strings with the first n node-to-hyperedge affiliations Layer.
        /// Used by the preview() CLI command.
        /// </summary>
        /// <param name="n">Number of edges to return (defaults to 10)</param>
        /// <returns>A list of strings</returns>
        public List<string> GetNFirstEdges(int n = 10)
        {
            List<string> lines = new(n);
            foreach (var kvp in _hyperedgeCollections)
            {
                if (lines.Count >= n)
                    break;
                uint nodeId = kvp.Key;
                HyperedgeCollection collection = kvp.Value;
                foreach (var hyperedge in collection.HyperEdges)
                {
                    if (lines.Count >= n)
                        break;
                    lines.Add($"{nodeId} -> {hyperedge.Name}");
                }
            }
            return lines;
        }
        #endregion


        #region Methods (private, internal)

        /// <summary>
        /// Support function.
        /// Returns a HashSet of Hyperedge objects that a node is part of. If the node lacks a collection of hyperedges, or
        /// if the collection is empty, return null.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <returns>HashSet of Hyperedges, or null </returns>
        internal HyperedgeCollection? GetNonEmptyHyperedgeCollection(uint nodeId)
        {
            if (HyperEdgeCollections.TryGetValue(nodeId, out var collection) && collection.HyperEdges.Count > 0)
                return collection;
            return null;
        }

        /// <summary>
        /// Retrieves a hyperedge with the specified name from the collection of all hyperedges.
        /// </summary>
        /// <param name="hyperName">The name of the hyperedge.</param>
        /// <returns>The hyperedge associated with the specified name, or null if no such hyperedge exists.</returns>
        internal Hyperedge? GetHyperedge(string hyperName)
        {
            if (AllHyperEdges.TryGetValue(hyperName, out var hyperedge))
                return hyperedge;
            return null;
        }

        /// <summary>
        /// Adds a Hyperedge with the specified name and optional array of node id members.
        /// Makes sure to filter out duplicate node ids for the Hyperedge.
        /// If there is already a Hyperedge with this name, the previous one is first removed before
        /// creating and adding the new one.
        /// </summary>
        /// <param name="hyperName">The name of the Hyperedge</param>
        /// <param name="nodeIds">Optional array of node ids for this hyperedge</param>
        /// <returns>An <see cref="OperationResult"/> informing how well this went.</returns>
        internal OperationResult AddHyperedge(string hyperName, uint[]? nodeIds)
        {
            if (AllHyperEdges.ContainsKey(hyperName))
                RemoveHyperedge(hyperName);
            Hyperedge hyperedge = new Hyperedge(hyperName);
            AllHyperEdges.Add(hyperName, hyperedge);
            if (nodeIds != null && nodeIds.Length > 0)
            {
                hyperedge.SetNodeIds([.. nodeIds.Distinct()]);
                foreach (uint nodeId in nodeIds)
                    AddHyperEdgeToNode(nodeId, hyperedge);
            }
            return OperationResult.Ok($"Added hyperedge '{hyperName}' (with {hyperedge.NbrNodes} nodes) in layer '{Name}'.");
        }

        /// <summary>
        /// Add a hyperedge reference to a node's collection of hyperedges. If the node has no collection
        /// of Hyperedge objects, it is first created.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="hyperedge">The hyperedge that the node is part of.</param>
        private void AddHyperEdgeToNode(uint nodeId, Hyperedge hyperedge)
        {
            if (HyperEdgeCollections.TryGetValue(nodeId, out var collection))
                collection.AddHyperEdge(hyperedge);
            else
                HyperEdgeCollections.Add(nodeId, new HyperedgeCollection(hyperedge));
        }

        /// <summary>
        /// Adds a hyperedge with the provided node ids. No validation: assumes that the named hyperedge
        /// does not exist, that nodeIds are proper etc. Used by FileSerializers
        /// </summary>
        /// <param name="hyperName">The name of the hyperedge to create and add</param>
        /// <param name="nodeIds">An array with node ids to the hyperedge</param>
        internal void _addHyperedge(string hyperName, uint[]? nodeIds)
        {
            Hyperedge hyperedge = nodeIds == null ? new Hyperedge(hyperName) : new Hyperedge(hyperName, nodeIds);
            AllHyperEdges[hyperName] = hyperedge;
            if (nodeIds != null)
                foreach (uint nodeId in nodeIds)
                    AddHyperEdgeToNode(nodeId, hyperedge);
        }

        /// <summary>
        /// Adds an empty hyperedge, i.e. a hyperedge with no nodeids (yet) connected to it.
        /// Assumes that the named hyperedge does not exist. Used by FileSerializers in case an empty
        /// Hyperedge is provided (which indeed could also be part of a 2-mode network)
        /// </summary>
        /// <param name="hyperName"></param>
        internal void _addHyperedge(string hyperName)
        {
            AllHyperEdges[hyperName] = new Hyperedge(hyperName);
        }

        /// <summary>
        /// Add an affiliation between a specific node and a hyperedge. If the hyperedge does not exist, it is created
        /// if the addMissingHyperedge setting is true. Adding affiliation both means that the hyperedge gets a reference
        /// to the nodeId, and the HyperedgeCollection belong to that nodeId gets a reference to the hyperedge.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="hyperName">The name of the hyperedge that the node is part of.</param>
        /// <param name="addMissingHyperedge">Indicates whether a non-existing hyperedge should be added.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult AddAffiliation(uint nodeId, string hyperName, bool addMissingHyperedge)
        {
            if (!AllHyperEdges.TryGetValue(hyperName, out var hyperedge))
            {
                if (!addMissingHyperedge)
                    return OperationResult.Fail("HyperedgeNotFound", $"Hyperedge '{hyperName}' not found in layer '{Name}'.");
                hyperedge = new Hyperedge(hyperName);
                AllHyperEdges.Add(hyperName, hyperedge);
            }
            AddHyperEdgeToNode(nodeId, hyperedge);
            if (!hyperedge.NodeIds.Contains(nodeId))
                hyperedge.AddNode(nodeId);
            return OperationResult.Ok($"Node '{nodeId}' affiliated to hyperedge '{hyperName}' in 2-mode layer '{Name}'.");
        }


        /// <summary>
        /// Adds an affiliation to a hyperedge. If the hyperedge does not exist, it is created.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="hyperName">The name of the affiliation.</param>
        internal void _addAffiliation(uint nodeId, string hyperName)
        {
            if (!AllHyperEdges.TryGetValue(hyperName, out var hyperedge))
            {
                hyperedge = new Hyperedge(hyperName);
                AllHyperEdges[hyperName] = hyperedge;
            }
            AddHyperEdgeToNode(nodeId, hyperedge);
            hyperedge.AddNode(nodeId);
        }

        /// <summary>
        /// Remove an affiliation between a specific node and a hyperedge. If the node is not affiliated with the hyperedge,
        /// i.e. something is internally wrong, then just continue as normal. Otherwise, remove the node id from the hyperedge
        /// and remove the nodal reference to this hyperedge.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="hyperName">The name of the hyperedge that the node is part of.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult RemoveAffiliation(uint nodeId, string hyperName)
        {
            if (!AllHyperEdges.TryGetValue(hyperName, out var hyperedge))
                return OperationResult.Fail("HyperedgeNotFound", $"Hyperedge '{hyperName}' not found in 2-mode layer '{Name}'.");
            if (HyperEdgeCollections.TryGetValue(nodeId, out var collection))
                collection.HyperEdges.Remove(hyperedge);
            if (!hyperedge.RemoveNode(nodeId))
                return OperationResult.Fail("ConstraintNodeNotAffiliated", $"Node '{nodeId}' not affiliated to hyperedge '{hyperName}' in 2-mode layer '{Name}'.");
            return OperationResult.Ok($"Node '{nodeId}' no longer affiliated to hyperedge '{hyperName}' in 2-mode layer '{Name}'.");
        }

        /// <summary>
        /// Removes a Hyperedge by its name. Also removes all references to this Hyperedge from the
        /// dictionary for node ids.
        /// </summary>
        /// <param name="hyperName">The name of the hyperedge.</param>
        /// <returns>An <see cref="OperationResult"/> informing how well this went.</returns>
        internal OperationResult RemoveHyperedge(string hyperName)
        {
            if (!AllHyperEdges.TryGetValue(hyperName, out var hyperedge))
                return OperationResult.Fail("HyperedgeNotFound", $"Hyperedge '{hyperName}' not found in 2-mode layer '{Name}'.");
            AllHyperEdges.Remove(hyperName);
            if (hyperedge.NodeIds.Count > 0)
            {
                foreach (uint nodeId in hyperedge.NodeIds)
                    RemoveHyperedgeFromNode(nodeId, hyperedge);
            }
            return OperationResult.Ok($"Hyperedge '{hyperName}' removed from 2-mode layer '{Name}'.");
        }

        /// <summary>
        /// Private support method to remove a Hyperedge from a node.
        /// Suppport method for method to remove a hyperedge.
        /// </summary>
        /// <param name="nodeId">The node id whose Hyperedge object is to be removed.</param>
        /// <param name="hyperedge">The Hyperedge to remove.</param>
        private void RemoveHyperedgeFromNode(uint nodeId, Hyperedge hyperedge)
        {
            if (HyperEdgeCollections.TryGetValue(nodeId, out var collection))
                collection.HyperEdges.Remove(hyperedge);
        }

        /// <summary>
        /// Retrieves the names of hyperedges associated with the specified node identifier.
        /// </summary>
        /// <param name="nodeId">The node id for which hyperedge names are to be retrieved.</param>
        /// <returns>An array of strings containing the names of hyperedges associated with the specified node. The array will be
        /// empty if no hyperedges are found for the given node identifier.</returns>
        internal string[] GetHyperedgeNames(uint nodeId)
        {
            if (!HyperEdgeCollections.TryGetValue(nodeId, out var hyperedgeCollection))
                return [];
            return hyperedgeCollection.HyperEdges.Select(he => he.Name).ToArray();
        }

        /// <summary>
        /// Retrieves a subset of hyperedge names, starting at the specified offset and limited to the given number of
        /// results.
        /// </summary>
        /// <param name="offset">The zero-based index at which to begin retrieving hyperedge names. If less than zero, the value is treated
        /// as zero.</param>
        /// <param name="limit">The maximum number of hyperedge names to retrieve. If less than zero, the value is treated as zero.</param>
        /// <returns>An array of strings containing the names of hyperedges, limited by the specified offset and limit. The array
        /// is empty if no hyperedges are available within the specified range.</returns>
        internal string[] GetAllHyperedgeNames(int offset, int limit)
        {
            offset = (offset < 0) ? 0 : offset;
            limit = (limit < 0) ? 0 : limit;
            return AllHyperEdges.Keys.Skip(offset).Take(limit).ToArray();
        }

        /// <summary>
        /// Creates and returns an empty copy of this layer.
        /// </summary>
        /// <returns>A <see cref="LayerTwoMode"/> object.</returns>
        private LayerTwoMode CreateEmptyCopy()
        {
            return new LayerTwoMode(this.Name);
        }
        #endregion
    }
}
