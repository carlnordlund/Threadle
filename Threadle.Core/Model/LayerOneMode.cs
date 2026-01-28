using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Represents a relational layer of 1-mode relations. Implements ILayer.
    /// Has additional properties and methods specific for 1-mode types of relations, e.g.
    /// specifies whether the layer has directional or symmetric ties, whether binary or valued, and
    /// whether self-ties are allowed.
    /// </summary>
    public class LayerOneMode : ILayer
    {
        #region Fields
        /// <summary>
        /// The collection of Edgesets associated by unique node nodeIdAlter.
        /// </summary>
        private Dictionary<uint, IEdgeset> _edgesets = [];

        /// <summary>
        /// The function used to create new edges.
        /// </summary>
        private Func<IEdgeset>? edgeSetFactory;
        #endregion


        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="LayerOneMode"/> class
        /// </summary>
        public LayerOneMode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LayerOneMode"/> class, with the specified properties.
        /// </summary>
        /// <param name="name">The name of the 1-mode layer.</param>
        /// <param name="directionality">A <see cref="EdgeDirectionality"/> value specifying whether the layer has directional or symmetric edges.</param>
        /// <param name="valueType">A <see cref="EdgeValueType"/> value specifying whether edges are binary or valued.</param>
        /// <param name="selfties">True if self-ties are allowed, false otherwise.</param>
        public LayerOneMode(string name, EdgeDirectionality directionality, EdgeType valueType, bool selfties)
        {
            Name = name;
            Directionality = directionality;
            EdgeValueType = valueType;
            Selfties = selfties;
            InitializeFactory();
        }
        #endregion


        #region Properties
        /// <summary>
        /// The name of the layer.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Directionality of edges in the layer.
        /// </summary>
        public EdgeDirectionality Directionality;

        /// <summary>
        /// Value type of edges in the layer.
        /// </summary>
        public EdgeType EdgeValueType;

        /// <summary>
        /// Boolean indicating whether selfties are allowed.
        /// </summary>
        public bool Selfties;

        /// <summary>
        /// Returns true if the layer is symmetric, otherwise false.
        /// </summary>
        public bool IsSymmetric => Directionality == EdgeDirectionality.Undirected;

        /// <summary>
        /// Returns true if the layer is directional, otherwise false.
        /// </summary>
        public bool IsDirectional => Directionality == EdgeDirectionality.Directed;

        /// <summary>
        /// Returns true if the layer has binary edges, otherwise false.
        /// </summary>
        public bool IsBinary => EdgeValueType == EdgeType.Binary;

        /// <summary>
        /// Returns true if the layer has valued edges, otherwise false.
        /// </summary>
        public bool IsValued => EdgeValueType == EdgeType.Valued;
        
        /// <summary>
        /// Returns the number of edges in the layer.
        /// </summary>
        public ulong NbrEdges
        {
            get
            {
                uint nbrConnections = 0;
                foreach ((uint nodeId, IEdgeset edgeset) in Edgesets)
                    nbrConnections += edgeset.NbrEdges;
                return Directionality == EdgeDirectionality.Directed ? nbrConnections : nbrConnections / 2;
            }
        }

        /// <summary>
        /// Returns metadata about the layer (as a dictionary of objects).
        /// </summary>
        public Dictionary<string, object> GetMetadata => new()
        {
            ["Name"] = Name,
            ["Mode"] = 1,
            ["Directionality"] = Directionality.ToString(),
            ["ValueType"] = EdgeValueType.ToString(),
            ["SelftiesAllowed"] = Selfties,
            ["NbrEdges"] = NbrEdges
        };

        /// <summary>
        /// Returns the dictionary of Edgeset objects by node nodeIdAlter.
        /// </summary>
        public Dictionary<uint, IEdgeset> Edgesets => _edgesets;

        /// <summary>
        /// Returns a string with metadata info about the layer
        /// </summary>
        public string GetLayerInfo => $" {Name} [1-mode: {EdgeValueType},{Directionality},{Selfties}); Nbr edges:{NbrEdges}]";
        #endregion


        #region Methods (public)
        /// <summary>
        /// Removes any edge in the Edgeset that includes this node nodeIdAlter.
        /// Used for cleaning up relational layers for invalid edges after a node has been removed.
        /// </summary>
        /// <param name="nodeId">The node nodeIdAlter whose edges are to be removed.</param>
        public void RemoveNodeEdges(uint nodeId)
        {
            Edgesets.Remove(nodeId);
            foreach (var edgeset in Edgesets.Values)
                edgeset.RemoveNodeEdgesInEdgeset(nodeId);
        }

        /// <summary>
        /// Gets the edge value between node1id and node2id if this exists (from node1id to node2id for directed layers).
        /// </summary>
        /// <param name="node1id">The first (source) node nodeIdAlter.</param>
        /// <param name="node2id">The second (destination) node nodeIdAlter.</param>
        /// <returns>The value of the edge if it exists, otherwise zero.</returns>
        public float GetEdgeValue(uint node1id, uint node2id)
        {
            if (!Edgesets.TryGetValue(node1id, out var edgeset))
                return 0f;
            return edgeset.GetOutboundPartnerEdgeValue(node2id);
        }

        /// <summary>
        /// Checks if an edge exists between node1id and node2id (from node1id to node2id for directed layers).
        /// </summary>
        /// <param name="node1id">The first (source) node nodeIdAlter.</param>
        /// <param name="node2id">The second (destination) node nodeIdAlter.</param>
        /// <returns>Returns true if there is an edge, false otherwise.</returns>
        public bool CheckEdgeExists(uint node1id, uint node2id)
        {
            if (!Edgesets.TryGetValue(node1id, out var edgeset))
                return false;
            return edgeset.CheckOutboundPartnerEdgeExists(node2id);
        }

        /// <summary>
        /// Returns an array of node ids in the edgeset for a particular node nodeIdAlter, i.e. the set of alters.
        /// For directional data, this could either be outbound, inbound, or both, as dictated by
        /// the <paramref name="edgeTraversal"/> parameter.
        /// </summary>
        /// <param name="edgeTraversal"><see cref="EdgeTraversal"/>Declares whether alters should be inbound, outbound, or both.</param>
        /// <returns>An array of node ids.</returns>
        public uint[] GetNodeAlters(uint nodeId, EdgeTraversal edgeTraversal)
        {
            if (!(Edgesets.TryGetValue(nodeId, out var edgeset)))
                return [];
            return edgeset.GetAlterIds(edgeTraversal);
        }

        /// <summary>
        /// Clears the layer, i.e. clears all Edgeset objects and removes these.
        /// </summary>
        public void ClearLayer()
        {
            foreach (IEdgeset edgeset in Edgesets.Values)
                edgeset.ClearEdges();
            Edgesets.Clear();
        }

        /// <summary>
        /// Returns a HashSet of all unique node ids mentioned in the Layer
        /// </summary>
        /// <returns>A HashSet of node ids.</returns>
        public HashSet<uint> GetMentionedNodeIds()
        {
            HashSet<uint> ids = [];
            foreach (IEdgeset edgeset in Edgesets.Values)
                ids.UnionWith(edgeset.GetAllNodeIds);
            return ids;
        }


        /// <summary>
        /// Create a filtered copy of this Layer that only keeps edges whose nodes are present in
        /// the provided Nodeset.
        /// </summary>
        /// <param name="nodeset">The Nodeset whose nodes to include</param>
        /// <returns>A copy of this Layer keeping the nodes that are in the Nodeset.</returns>
        public ILayer CreateFilteredCopy(Nodeset nodeset)
        {
            HashSet<uint> nodeIds = [.. nodeset.NodeIdArray];
            LayerOneMode layerCopy = CreateEmptyCopy();

            foreach ((uint nodeId, IEdgeset edgeset) in Edgesets)
            {
                if (!nodeIds.Contains(nodeId))
                    continue;
                layerCopy.Edgesets.Add(nodeId, edgeset.CreateFilteredCopy(nodeIds));
            }
            return layerCopy;
        }

        /// <summary>
        /// Returns a list of strings with the first n edges in the Layer. Used by the preview() CLI command.
        /// </summary>
        /// <param name="n">Number of edges to return (defaults to 10)</param>
        /// <returns>A list of strings</returns>
        public List<string> GetNFirstEdges(int n = 10)
        {
            List<string> edges = new(n);
            foreach (var kvp in _edgesets)
            {
                uint egoNodeId = kvp.Key;
                IEdgeset edgeset = kvp.Value;
                int remaining = n - edges.Count;
                if (remaining <= 0)
                    break;
                List<string> edgeLines = edgeset.FormatEdges(egoNodeId, remaining);
                edges.AddRange(edgeLines);
            }

            return edges;
        }
        #endregion


        #region Methods (private, internal)
        /// <summary>
        /// Support method attempting to initialize the edgesetFactory, i.e. the methods that is used to create new Edgeset instances.
        /// </summary>
        private void InitializeFactory()
        {
            if (IsBinary)
            {
                if (IsDirectional)
                    edgeSetFactory = () => new EdgesetBinaryDirectional();
                else
                    edgeSetFactory = () => new EdgesetBinarySymmetric();
            }
            else if (IsValued) {
                if (IsDirectional)
                    edgeSetFactory = () => new EdgesetValuedDirectional();
                else
                    edgeSetFactory = () => new EdgesetValuedSymmetric();
            }
        }

        /// <summary>
        /// Method to add an edge between two nodes. If the layer contains directional edges, the edge goes
        /// from node1id to node2id. The edge value is optional for layers with binary edges.
        /// If Directional and UserSetting is set to only add outbound edges, no inbound edge is stored at the destination node.
        /// Returns an OperationResult informing how it all went.
        /// </summary>
        /// <param name="node1id">The first (source) bode nodeIdAlter.</param>
        /// <param name="node2id">The second (destination) node nodeIdAlter.</param>
        /// <param name="value">The value of the edge (defaults to 1; moot for binary layers).</param>
        /// <returns>An <see cref="OperationResult"/> informing how well this went.</returns>
        internal OperationResult AddEdge(uint node1id, uint node2id, float value = 1)
        {
            if (node1id == node2id && !Selfties)
                return OperationResult.Fail("ConstraintSelftiesNotAllowed",$"Layer {Name} does not allow for selfties.");
            IEdgeset edgeSetNode1 = GetOrCreateEdgeset(node1id);
            IEdgeset edgeSetNode2 = GetOrCreateEdgeset(node2id);
            if (IsSymmetric || !UserSettings.OnlyOutboundEdges)
                if (!edgeSetNode2.AddInboundEdge(node1id, value).Success)
                    return OperationResult.Fail("EdgeAlreadyExists", $"Inbound edge to {node2id} from {node1id} already exists.");
            if (!edgeSetNode1.AddOutboundEdge(node2id, value).Success)
                return OperationResult.Fail("EdgeAlreadyExists", $"Outbound edge from {node1id} to {node2id} already exists.");
            return OperationResult.Ok($"Added edge {Misc.BetweenFromToText(Directionality, node1id, node2id)} (value={value}) in layer '{Name}'.");
        }

        /// <summary>
        /// Add edges between an ego node and array of alter nodes without any validation.
        /// Used by binary reader.
        /// Note that it is quite cleverly constructed: read extensive comments.
        /// </summary>
        /// <param name="nodeIdEgo">The id of ego node.</param>
        /// <param name="nodeIdsAlters">Array of id to alter nodes that ego is connected to.</param>
        internal void _addBinaryEdges(uint nodeIdEgo, uint[] nodeIdsAlters)
        {
            IEdgeset edgeSetEgo = GetOrCreateEdgeset(nodeIdEgo);
            if (IsSymmetric || !UserSettings.OnlyOutboundEdges)
            {
                // So two reasons to be here:
                // Layer is symmetric: we should then add references to the other node in both nodes. As it is
                // symmetric, it doesn't matter if we use _addOutbound... or _addInbound...: both will add partnerNodeId
                // to the undirectional _connection.
                // If it is NOT symmetric, we are here because it is directional (not symmetric) and the UserSetting to
                // only store outbound directional edges is turned off (False). So it is now directional, and we should
                // add an outgoing edge from ego to alter, and an ingoing edge to alter from ego.
                // Which we do with the same methods as we used for the symmetric case, but this time they do differ!
                foreach (uint nodeIdAlter in nodeIdsAlters)
                {
                    edgeSetEgo._addOutboundEdge(nodeIdAlter, 1);
                    GetOrCreateEdgeset(nodeIdAlter)._addInboundEdge(nodeIdEgo, 1);
                }
            }
            else
            {
                // So we are here because the layer is directional and UserSetting to only store outbound is turned on (true)
                // I.e. neither symmetric or not only-outbound triggered above. This means: directional, and only store outbound
                // ties. Which we do:
                foreach (uint idAlter in nodeIdsAlters)
                    edgeSetEgo._addOutboundEdge(idAlter, 1);
            }
        }

        /// <summary>
        /// Add an edge between two nodes - by default a binary value but can be specified.
        /// Always add an outbound from node1 to node2. Add an inbound to node2 from node1
        /// as long as we don't have OnlyOutboundEdges activated - or if it is symmetric
        /// </summary>
        /// <param name="node1id">The first node id.</param>
        /// <param name="node2id">The second node id.</param>
        /// <param name="value">The value of the edge (defaults to 1)</param>
        internal void _addEdge(uint node1id, uint node2id, float value = 1)
        {
            GetOrCreateEdgeset(node1id)._addOutboundEdge(node2id, value);
            if (IsSymmetric || !UserSettings.OnlyOutboundEdges)
                GetOrCreateEdgeset(node2id)._addInboundEdge(node1id, value);
        }

        /// <summary>
        /// Add valued edges between an ego node and alter nodes without any validation.
        /// Used by binaryreader.
        /// See <see cref="_addBinaryEdges(uint, uint[])"/> for details on how it works!
        /// </summary>
        /// <param name="nodeIdEgo">The id of ego node.</param>
        /// <param name="nodeIdsAlters">A List of tuples containing node alter ids and edge values</param>
        internal void _addValuedEdges(uint nodeIdEgo, List<(uint alterId, float value)> nodeIdsAlters)
        {
            IEdgeset edgeSetEgo = GetOrCreateEdgeset(nodeIdEgo);
            if (IsSymmetric || !UserSettings.OnlyOutboundEdges)
            {
                foreach ((uint alterId, float value) in nodeIdsAlters)
                {
                    edgeSetEgo._addOutboundEdge(alterId, value);
                    GetOrCreateEdgeset(alterId)._addInboundEdge(nodeIdEgo, value);
                }
            }
            else
                foreach ((uint alterId, float value) in nodeIdsAlters)
                    edgeSetEgo.AddOutboundEdge(alterId, value);
        }

        /// <summary>
        /// Removes an (the) edge between node1id and node2id. Returns a fail if no such edge is found.
        /// (First checks if there indeed are any Edgeset recorded for these, then checks if there are any corresponding edges recorded)
        /// </summary>
        /// <param name="node1id">The first (source) node nodeIdAlter.</param>
        /// <param name="node2id">The second (destination) node nodeIdAlter.</param>
        /// <returns>An <see cref="OperationResult"/> informing how well this went.</returns>
        internal OperationResult RemoveEdge(uint node1id, uint node2id)
        {
            if (!Edgesets.TryGetValue(node1id, out var edgesetNode1) || !Edgesets.TryGetValue(node2id, out var edgesetNode2))
                return OperationResult.Fail("EdgeNotFound", $"No edge {Misc.BetweenFromToText(Directionality, node1id, node2id)} in layer '{Name}' found.");
            if (!edgesetNode1.RemoveOutboundEdge(node2id).Success || !edgesetNode2.RemoveInboundEdge(node1id).Success)
                return OperationResult.Fail("EdgeNotFound", $"No edge {Misc.BetweenFromToText(Directionality, node1id, node2id)} in layer '{Name}' found.");
            return OperationResult.Ok($"Removed edge {Misc.BetweenFromToText(Directionality, node1id, node2id)} in layer '{Name}'.");
        }

        /// <summary>
        /// Gets an existing Edgeset object for a node nodeIdAlter, or creates it first if not existing.
        /// </summary>
        /// <param name="nodeId">The node nodeIdAlter whose Edgeset is to be obtained.</param>
        /// <returns></returns>
        internal IEdgeset GetOrCreateEdgeset(uint nodeId)
        {
            if (Edgesets.TryGetValue(nodeId, out var edgeset))
                return edgeset;
            IEdgeset newEdgeset = edgeSetFactory!();
            Edgesets[nodeId] = newEdgeset;
            return newEdgeset;
        }

        /// <summary>
        /// Non-private method to try to initialize the Edgeset-creating factory.
        /// </summary>
        internal void TryInitFactory()
        {
            InitializeFactory();
        }

        /// <summary>
        /// Gets the outdegree (i.e. number of outbound edges) for a node nodeIdAlter.
        /// </summary>
        /// <param name="nodeId">The node nodeIdAlter.</param>
        /// <returns>The number of outbound edges this node nodeIdAlter has.</returns>
        internal uint GetOutDegree(uint nodeId)
        {
            if (!Edgesets.TryGetValue(nodeId, out var edgeset))
                return 0;
            return edgeset.NbrOutboundEdges;
        }

        /// <summary>
        /// Gets the indegree (i.e. number of inbound edges) for a node nodeIdAlter.
        /// </summary>
        /// <param name="nodeId">The node nodeIdAlter.</param>
        /// <returns>The number of inbound edges this node nodeIdAlter has.</returns>
        internal uint GetInDegree(uint nodeId)
        {
            if (!Edgesets.TryGetValue(nodeId, out var edgeset))
                return 0;
            return edgeset.NbrInboundEdges;
        }

        /// <summary>
        /// Creates and returns an empty copy of this layer.
        /// </summary>
        /// <returns></returns>
        private LayerOneMode CreateEmptyCopy()
        {
            return new LayerOneMode(this.Name, this.Directionality, this.EdgeValueType, this.Selfties);
        }

        /// <summary>
        /// Initialize the capacity of the _edgesets dictionary.
        /// Suitable when loading from file.
        /// </summary>
        /// <param name="nbrEdgesets"></param>
        internal void _initSizeEdgesetDictionary(int nbrEdgesets)
        {
            _edgesets = new(nbrEdgesets);
        }

        /// <summary>
        /// Initialize the capacity of the _edgesets dictionary, also creating
        /// ready-to-use, empty IEdgeset instances from the specific factory method for the
        /// provided array of node ids.
        /// Also sets the expected capacity for these IEdgeset containers.
        /// Suitable when generating random networks such as Erdös-Renyi
        /// </summary>
        /// <param name="nodeIds">Array of node ids to initialize (using its length for _edgesets capacity)</param>
        /// <param name="edgesetCapacity">The expected capacity of edge IEdgeset.</param>
        internal void _initEdgesets(uint[] nodeIds, int edgesetCapacity)
        {
            _edgesets = new(nodeIds.Length);
            foreach (var id in nodeIds)
            {
                _edgesets[id] = edgeSetFactory!();
                _edgesets[id]._setCapacity(edgesetCapacity);
            }
        }

        /// <summary>
        /// Goes through all Edgesets in the layer and removes any duplicate edges (i.e. multiedges)
        /// Used after the LayerImporter is finished, where it is more likely to encounter duplicate edges
        /// E.g. if a directional edgelist is imported to a symmetric layer
        /// </summary>
        internal void _deduplicateEdgesets()
        {
            foreach (IEdgeset edgeset in Edgesets.Values)
                edgeset._deduplicate();
        }

        /// <summary>
        /// Goes through all Edgesets in the layer and sorts the partnerNodeIds by id
        /// Used after having symmetrized, as that could result in the order of node Ids being
        /// non-incremental
        /// </summary>
        internal void _sortEdgesets()
        {
            foreach (IEdgeset edgeset in Edgesets.Values)
                edgeset._sort();
        }
        #endregion
    }
}
