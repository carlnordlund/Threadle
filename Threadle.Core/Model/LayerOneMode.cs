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
    public class LayerOneMode : ILayer, ILayerOneMode
    {
        #region Fields
        /// <summary>
        /// The collection of Edgesets associated by unique nodes.
        /// </summary>
        private Dictionary<uint, IEdgeset> _edgesets = [];

        /// <summary>
        /// The function used to create new edges.
        /// </summary>
        private Func<IEdgeset>? edgeSetFactory;
        #endregion


        #region Constructors
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
        public EdgeDirectionality Directionality { get; }

        /// <summary>
        /// Value type of edges in the layer.
        /// </summary>
        public EdgeType EdgeValueType { get; }

        /// <summary>
        /// Boolean indicating whether selfties are allowed.
        /// </summary>
        public bool Selfties { get; }

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
                ulong nbrConnections = 0;
                foreach ((uint nodeId, IEdgeset edgeset) in Edgesets)
                    nbrConnections += edgeset.NbrEdges;
                if (Directionality == EdgeDirectionality.Directed)
                    return nbrConnections;

                // Undirected: an ordinary edge is stored once per endpoint (so divide the total by
                // 2), but a self-loop is stored only once overall (on its one node), so it must not
                // be halved along with everything else.
                if (!Selfties)
                    return nbrConnections / 2;
                ulong selfLoopCount = 0;
                foreach ((uint nodeId, IEdgeset edgeset) in Edgesets)
                    if (edgeset.CheckOutboundPartnerEdgeExists(nodeId))
                        selfLoopCount++;
                return (nbrConnections - selfLoopCount) / 2 + selfLoopCount;
            }
        }

        /// <summary>
        /// Returns metadata about the layer (as a dictionary of objects).
        /// </summary>
        public Dictionary<string, object> GetMetadata => new()
        {
            ["Name"] = Name,
            ["Mode"] = 1,
            ["Static"] = false,
            ["Directionality"] = Directionality.ToString(),
            ["ValueType"] = EdgeValueType.ToString(),
            ["SelftiesAllowed"] = Selfties,
            ["NbrEdges"] = NbrEdges
        };

        /// <summary>
        /// Returns the dictionary of Edgeset objects by node id.
        /// </summary>
        public Dictionary<uint, IEdgeset> Edgesets => _edgesets;

        /// <summary>
        /// Returns a string with metadata info about the layer
        /// </summary>
        public string GetLayerInfo => $" {Name} [1-mode; {EdgeValueType},{Directionality},{Selfties}; Nbr edges:{NbrEdges}]";

        public bool IsStatic => false;
        #endregion


        #region Methods (public)
        public static LayerOneMode FromStatic(LayerOneModeStatic source)
        {
            var layer = new LayerOneMode(source.Name, source.Directionality, source.EdgeValueType, source.Selfties);
            layer._initSizeEdgesetDictionary(source.NodeCount);
            foreach (var (egoId, alters, values) in source.GetAllEgoData())
            {
                ReadOnlySpan<uint> altersSpan = alters.Span;
                if (!values.IsEmpty)
                {
                    var valuedAlters = new List<(uint, float)>(altersSpan.Length);
                    ReadOnlySpan<float> valSpan = values.Span;
                    for (int i = 0; i < altersSpan.Length; i++)
                        valuedAlters.Add((altersSpan[i], valSpan[i]));
                    layer._addValuedEdges(egoId, valuedAlters);
                }
                else
                {
                    layer._addBinaryEdges(egoId, alters.ToArray());
                }
            }

            // GetAllEgoData() never yields self-loops for symmetric layers: add them explicitly, once
            // each, via _addEdge (which correctly guards against double-adding a tie whose two
            // endpoints are the same node, unlike _addBinaryEdges/_addValuedEdges above).
            if (source.IsSymmetric && source.Selfties)
                foreach (uint nodeId in source.NodeIds)
                    if (source.CheckEdgeExists(nodeId, nodeId))
                        layer._addEdge(nodeId, nodeId, source.GetEdgeValue(nodeId, nodeId));

            return layer;
        }



        /// <summary>
        /// Removes any edge in the Edgeset that includes this node id.
        /// Used for cleaning up relational layers for invalid edges after a node has been removed.
        /// </summary>
        /// <param name="nodeId">The node id whose edges are to be removed.</param>
        public void RemoveNodeEdges(uint nodeId)
        {
            Edgesets.Remove(nodeId);
            foreach (var edgeset in Edgesets.Values)
                edgeset.RemoveNodeEdgesInEdgeset(nodeId);
        }

        /// <summary>
        /// Gets the edge value between node1Id and node2Id if this exists (from node1Id to node2Id for directed layers).
        /// </summary>
        /// <param name="node1Id">The first (source) node id.</param>
        /// <param name="node2Id">The second (destination) node id.</param>
        /// <returns>The value of the edge if it exists, otherwise zero.</returns>
        public float GetEdgeValue(uint node1Id, uint node2Id)
        {
            if (!Edgesets.TryGetValue(node1Id, out var edgeset))
                return 0f;
            return edgeset.GetOutboundPartnerEdgeValue(node2Id);
        }

        /// <summary>
        /// Checks if an edge exists between node1Id and node2Id (from node1Id to node2Id for directed layers).
        /// </summary>
        /// <param name="node1Id">The first (source) node id.</param>
        /// <param name="node2Id">The second (destination) node id.</param>
        /// <returns>Returns true if there is an edge, false otherwise.</returns>
        public bool CheckEdgeExists(uint node1Id, uint node2Id)
        {
            if (!Edgesets.TryGetValue(node1Id, out var edgeset))
                return false;
            return edgeset.CheckOutboundPartnerEdgeExists(node2Id);
        }

        /// <summary>
        /// Returns an array of node ids in the edgeset for a particular node id, i.e. the set of alters.
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
        /// Returns alter node ids together with their associated edge weights for use in weighted random selection.
        /// </summary>
        /// <param name="nodeId">The ego node id.</param>
        /// <param name="edgeTraversal">Edge traversal direction. Ignored for undirected 1-mode layers.</param>
        /// <returns>A tuple of parallel alter-id and weight memory regions. Weights may be empty for binary layers.</returns>
        public (ReadOnlyMemory<uint> alters, ReadOnlyMemory<float> weights) GetNodeAltersWithWeights(uint nodeId, EdgeTraversal edgeTraversal)
        {
            if (!Edgesets.TryGetValue(nodeId, out var edgeset))
                return (ReadOnlyMemory<uint>.Empty, ReadOnlyMemory<float>.Empty);

            // Binary: return alter IDs with empty weights; caller treats each as weight 1.0f
            if (edgeset is not IEdgesetValued valuedEdgeset)
                return (edgeset.GetAlterIds(edgeTraversal), ReadOnlyMemory<float>.Empty);

            // Symmetric (undirected valued): direction irrelevant — use outbound connections
            if (edgeset is IEdgesetSymmetric)
            {
                var conns = valuedEdgeset.GetOutboundConnections;
                uint[] a = new uint[conns.Count]; float[] w = new float[conns.Count];
                for (int i = 0; i < conns.Count; i++) { a[i] = conns[i].partnerNodeId; w[i] = conns[i].value; }
                return (a, w);
            }

            // Directional valued — out only
            if (edgeTraversal == EdgeTraversal.Out)
            {
                var conns = valuedEdgeset.GetOutboundConnections;
                uint[] a = new uint[conns.Count]; float[] w = new float[conns.Count];
                for (int i = 0; i < conns.Count; i++) { a[i] = conns[i].partnerNodeId; w[i] = conns[i].value; }
                return (a, w);
            }

            // Directional valued — in only
            if (edgeTraversal == EdgeTraversal.In)
            {
                var conns = valuedEdgeset.GetInboundConnections;
                uint[] a = new uint[conns.Count]; float[] w = new float[conns.Count];
                for (int i = 0; i < conns.Count; i++) { a[i] = conns[i].partnerNodeId; w[i] = conns[i].value; }
                return (a, w);
            }

            // Directional valued — both: combine, sum weights for bidirectional pairs
            {
                var outConns = valuedEdgeset.GetOutboundConnections;
                var inConns = valuedEdgeset.GetInboundConnections;
                var combined = new Dictionary<uint, float>(outConns.Count + inConns.Count);
                foreach (var c in outConns)
                    combined[c.partnerNodeId] = combined.GetValueOrDefault(c.partnerNodeId) + c.value;
                foreach (var c in inConns)
                    combined[c.partnerNodeId] = combined.GetValueOrDefault(c.partnerNodeId) + c.value;
                uint[] a = new uint[combined.Count]; float[] w = new float[combined.Count];
                int idx = 0;
                foreach (var (alterId, weight) in combined) { a[idx] = alterId; w[idx++] = weight; }
                return (a, w);
            }
        }

        /// <inheritdoc/>
        public void VisitAlters(uint nodeId, EdgeTraversal edgeTraversal, Action<uint> visitAlter)
        {
            if (!Edgesets.TryGetValue(nodeId, out var edgeset))
                return;

            if (!IsDirectional || edgeTraversal == EdgeTraversal.Out || edgeTraversal == EdgeTraversal.Both)
            {
                if (edgeset is IEdgesetValued valuedOut)
                    foreach (var c in valuedOut.GetOutboundConnections)
                        visitAlter(c.partnerNodeId);
                else
                    foreach (uint v in edgeset.GetOutboundNodeIds)
                        visitAlter(v);
            }

            if (IsDirectional && (edgeTraversal == EdgeTraversal.In || edgeTraversal == EdgeTraversal.Both))
            {
                if (edgeset is IEdgesetValued valuedIn)
                    foreach (var c in valuedIn.GetInboundConnections)
                        visitAlter(c.partnerNodeId);
                else
                    foreach (uint v in edgeset.GetInboundNodeIds)
                        visitAlter(v);
            }
        }


        /// <summary>
        /// Retrieves a collection of edges with their associated values, starting from a specified offset and limited
        /// to a maximum number of results.
        /// </summary>
        /// <param name="offset">The number of edges to skip before beginning to collect results. Must be greater than or equal to 0.</param>
        /// <param name="limit">The maximum number of edges to return. Must be greater than 0.</param>
        /// <returns>A list of dictionaries, each containing the identifiers of two connected nodes and the associated value. The
        /// list may be empty if no edges are available after applying the offset.</returns>
        public List<Dictionary<string, object>> GetAllEdges(int offset = 0, int limit = 1000)
        {
            List<Dictionary<string, object>> edges = [];
            int skipped = 0;
            bool includeSelfties = IsSymmetric && Selfties;
            foreach (var kvp in _edgesets)
            {
                uint egoId = kvp.Key;

                // GetOutboundEdgesWithValues() never yields self-loops for symmetric layers
                // (a self-loop has no "other side" alterId > egoId to be found via): add it
                // explicitly, once, from the one edgeset it is stored in.
                if (includeSelfties && CheckEdgeExists(egoId, egoId))
                {
                    if (skipped < offset)
                        skipped++;
                    else if (edges.Count < limit)
                        edges.Add(new Dictionary<string, object>
                        {
                            ["node1"] = egoId,
                            ["node2"] = egoId,
                            ["value"] = GetEdgeValue(egoId, egoId)
                        });
                    else
                        return edges;
                }

                foreach (var (alterId, value) in kvp.Value.GetOutboundEdgesWithValues(egoId))
                {
                    if (skipped < offset)
                    {
                        skipped++;
                        continue;
                    }

                    if (edges.Count >= limit)
                        return edges;

                    edges.Add(new Dictionary<string, object>
                    {
                        ["node1"] = egoId,
                        ["node2"] = alterId,
                        ["value"] = value
                    });
                }
            }
            return edges;
        }

        /// <summary>
        /// Iterates all ego nodes with their outbound alters and edge values.
        /// For undirected layers, each edge is yielded only once (from the lower node id).
        /// <c>values</c> is empty for binary layers.
        /// </summary>
        public IEnumerable<(uint egoId, ReadOnlyMemory<uint> alters, ReadOnlyMemory<float> values)> GetAllEgoData()
        {
            foreach (var (egoId, edgeset) in _edgesets)
            {
                var alterList = new List<uint>();
                List<float>? valueList = IsValued ? [] : null;
                foreach (var (alterId, val) in edgeset.GetOutboundEdgesWithValues(egoId))
                {
                    alterList.Add(alterId);
                    valueList?.Add(val);
                }
                yield return (egoId, alterList.ToArray(), valueList?.ToArray());
            }
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
        /// Create a filtered copy of this Layer that only keeps edges whose nodes are present in
        /// the provided Nodeset.
        /// </summary>
        /// <param name="nodeset">The Nodeset whose nodes to include</param>
        /// <returns>A copy of this Layer keeping the nodes that are in the Nodeset.</returns>
        public ILayer CreateFilteredCopy(Nodeset nodeset)
        {
            HashSet<uint> nodeIds = [.. nodeset.NodeIdArray];
            LayerOneMode layerCopy = new LayerOneMode(this.Name + "_filtered", this.Directionality, this.EdgeValueType, this.Selfties);

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
            bool includeSelfties = IsSymmetric && Selfties;
            foreach (var kvp in _edgesets)
            {
                uint egoNodeId = kvp.Key;
                IEdgeset edgeset = kvp.Value;
                int remaining = n - edges.Count;
                if (remaining <= 0)
                    break;

                // FormatEdges() only finds the other side (partnerId > egoNodeId) of an
                // ordinary undirected edge; a self-loop has no other side, so it is never
                // reported by FormatEdges() and must be added explicitly here.
                if (includeSelfties && CheckEdgeExists(egoNodeId, egoNodeId))
                {
                    edges.Add(IsBinary
                        ? $"{egoNodeId} <-> {egoNodeId}"
                        : $"{egoNodeId} <-> {egoNodeId} ({GetEdgeValue(egoNodeId, egoNodeId)})");
                    remaining--;
                    if (remaining <= 0)
                        break;
                }

                List<string> edgeLines = edgeset.FormatEdges(egoNodeId, remaining);
                edges.AddRange(edgeLines);
            }

            return edges;
        }

        public long GetEstimatedBytes()
        {
            int N = _edgesets.Count;
            long bytes = (long)N * 20 + (long)(N / 0.72 + 1) * 4;
            foreach (var es in _edgesets.Values)
                bytes += 72 + (long)es.NbrEdges * 4;
            return bytes;
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
            else if (IsValued)
            {
                if (IsDirectional)
                    edgeSetFactory = () => new EdgesetValuedDirectional();
                else
                    edgeSetFactory = () => new EdgesetValuedSymmetric();
            }
        }

        /// <summary>
        /// Method to add an edge between two nodes. If the layer contains directional edges, the edge goes
        /// from node1Id to node2Id. The edge value is optional for layers with binary edges.
        /// Returns an OperationResult informing how it all went.
        /// </summary>
        /// <param name="node1Id">The first (source) node id.</param>
        /// <param name="node2Id">The second (destination) node id.</param>
        /// <param name="value">The value of the edge (defaults to 1; moot for binary layers).</param>
        /// <returns>An <see cref="OperationResult"/> informing how well this went.</returns>
        internal OperationResult AddEdge(uint node1Id, uint node2Id, float value = 1)
        {
            if (node1Id == node2Id && !Selfties)
                return OperationResult.Fail("ConstraintSelftiesNotAllowed", $"Layer {Name} does not allow for selfties.");
            IEdgeset edgeSetNode1 = GetOrCreateEdgeset(node1Id);
            IEdgeset edgeSetNode2 = GetOrCreateEdgeset(node2Id);

            bool needsInbound = node1Id != node2Id || !IsSymmetric;

            if (needsInbound)
                if (!edgeSetNode2.AddInboundEdge(node1Id, value).Success)
                    return OperationResult.Fail("EdgeAlreadyExists", $"Inbound edge to {node2Id} from {node1Id} already exists.");

            if (!edgeSetNode1.AddOutboundEdge(node2Id, value).Success)
            {
                if (needsInbound)
                    edgeSetNode2.RemoveInboundEdge(node1Id);
                return OperationResult.Fail("EdgeAlreadyExists", $"Outbound edge from {node1Id} to {node2Id} already exists.");
            }

            return OperationResult.Ok($"Added edge {Misc.BetweenFromToText(Directionality, node1Id, node2Id)} (value={value}) in layer '{Name}'.");
        }

        /// <summary>
        /// Add edges between an ego node and array of alter nodes without any validation.
        /// Used by binary reader.
        /// Note that it is quite cleverly constructed: read extensive comments.
        /// </summary>
        /// <param name="egoNodeId">The id of ego node.</param>
        /// <param name="nodeIdsAlters">Array of id to alter nodes that ego is connected to.</param>
        internal void _addBinaryEdges(uint egoNodeId, uint[] nodeIdsAlters)
        {
            IEdgeset edgeSetEgo = GetOrCreateEdgeset(egoNodeId);
            // Whether symmetric or directional, every edge gets an outbound entry at ego and an
            // inbound entry at alter - for a symmetric layer, _addOutbound.../_addInbound... both
            // add the alter to the same undirectional _connection, so which one is called doesn't
            // matter there.
            foreach (uint nodeIdAlter in nodeIdsAlters)
            {
                edgeSetEgo._addOutboundEdge(nodeIdAlter, 1);
                GetOrCreateEdgeset(nodeIdAlter)._addInboundEdge(egoNodeId, 1);
            }
        }

        /// <summary>
        /// Add an edge between two nodes - by default a binary value but can be specified.
        /// Always add an outbound from node1 to node2, and an inbound to node2 from node1,
        /// except for a symmetric self-tie (stored once, in the single edgeset shared by both
        /// "sides").
        /// </summary>
        /// <param name="node1Id">The first node id.</param>
        /// <param name="node2Id">The second node id.</param>
        /// <param name="value">The value of the edge (defaults to 1)</param>
        internal void _addEdge(uint node1Id, uint node2Id, float value = 1)
        {
            GetOrCreateEdgeset(node1Id)._addOutboundEdge(node2Id, value);
            if (node1Id != node2Id || !IsSymmetric)
                GetOrCreateEdgeset(node2Id)._addInboundEdge(node1Id, value);
        }

        /// <summary>
        /// Add valued edges between an ego node and alter nodes without any validation.
        /// Used by binaryreader.
        /// See <see cref="_addBinaryEdges(uint, uint[])"/> for details on how it works!
        /// </summary>
        /// <param name="egoNodeId">The id of ego node.</param>
        /// <param name="nodeIdsAlters">A List of tuples containing node alter ids and edge values</param>
        internal void _addValuedEdges(uint egoNodeId, List<(uint alterId, float value)> nodeIdsAlters)
        {
            IEdgeset edgeSetEgo = GetOrCreateEdgeset(egoNodeId);
            foreach ((uint alterId, float value) in nodeIdsAlters)
            {
                edgeSetEgo._addOutboundEdge(alterId, value);
                GetOrCreateEdgeset(alterId)._addInboundEdge(egoNodeId, value);
            }
        }

        /// <summary>
        /// Removes an (the) edge between node1Id and node2Id. Returns a fail if no such edge is found.
        /// (First checks if there indeed are any Edgeset recorded for these, then checks if there are any corresponding edges recorded)
        /// </summary>
        /// <param name="node1Id">The first (source) node id.</param>
        /// <param name="node2Id">The second (destination) node id.</param>
        /// <returns>An <see cref="OperationResult"/> informing how well this went.</returns>
        internal OperationResult RemoveEdge(uint node1Id, uint node2Id)
        {
            if (!Edgesets.TryGetValue(node1Id, out var edgesetNode1) || !Edgesets.TryGetValue(node2Id, out var edgesetNode2))
                return OperationResult.Fail("EdgeNotFound", $"No edge {Misc.BetweenFromToText(Directionality, node1Id, node2Id)} in layer '{Name}' found.");

            // A self-tie in a symmetric layer is stored once, in the single edgeset shared by
            // both "sides" (node1Id == node2Id, so edgesetNode1 and edgesetNode2 are the same
            // object): removing it via both RemoveOutboundEdge and RemoveInboundEdge would try
            // to remove the same already-gone entry twice, misreporting failure on the second
            // attempt even though the first one actually succeeded. Directed self-loops don't
            // have this issue - they're stored in separate outbound/inbound lists - so only the
            // symmetric case needs the single-call path.
            bool removed = IsSymmetric && node1Id == node2Id
                ? edgesetNode1.RemoveOutboundEdge(node2Id).Success
                : edgesetNode1.RemoveOutboundEdge(node2Id).Success && edgesetNode2.RemoveInboundEdge(node1Id).Success;

            if (!removed)
                return OperationResult.Fail("EdgeNotFound", $"No edge {Misc.BetweenFromToText(Directionality, node1Id, node2Id)} in layer '{Name}' found.");
            return OperationResult.Ok($"Removed edge {Misc.BetweenFromToText(Directionality, node1Id, node2Id)} in layer '{Name}'.");
        }

        /// <summary>
        /// Gets an existing Edgeset object for a node id, or creates it first if not existing.
        /// </summary>
        /// <param name="nodeId">The node id whose Edgeset is to be obtained.</param>
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
        /// Gets the outdegree (i.e. number of outbound edges) for a node id.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <returns>The number of outbound edges this node id has.</returns>
        public uint GetOutDegree(uint nodeId)
        {
            if (!Edgesets.TryGetValue(nodeId, out var edgeset))
                return 0;
            return edgeset.NbrOutboundEdges;
        }

        /// <summary>
        /// Gets the indegree (i.e. number of inbound edges) for a node id.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <returns>The number of inbound edges this node id has.</returns>
        public uint GetInDegree(uint nodeId)
        {
            if (!Edgesets.TryGetValue(nodeId, out var edgeset))
                return 0;
            return edgeset.NbrInboundEdges;
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