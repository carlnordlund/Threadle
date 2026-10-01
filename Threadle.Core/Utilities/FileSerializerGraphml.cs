using System.Globalization;
using System.Text;
using System.Xml;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Class for importing and exporting networks in the GraphML format (http://graphml.graphdrawing.org/), as used
    /// by e.g. igraph, networkx, Gephi, Cytoscape and yEd.
    /// Mapping between GraphML and Threadle:
    /// - GraphML nodes are Threadle nodes. If all GraphML node ids are unsigned integers, they are used as node ids.
    ///   Otherwise, nodes get new node ids (0, 1, 2, ... in document order), with the GraphML node id stored in a
    ///   string node attribute.
    /// - GraphML node attributes (keys for 'node' or 'all') become node attributes. The GraphML types 'int' and 'long'
    ///   become Int, 'float' and 'double' become Float, 'boolean' becomes Bool, and 'string' becomes String.
    /// - GraphML edges become edges in 1-mode layers. If edges have a layer attribute, there is one layer per distinct
    ///   value; otherwise all edges are in a single layer. A layer is directed if its edges are directed (per edge, or
    ///   by the 'edgedefault' of the graph), valued if its edges have a weight attribute, and allows selfties if there
    ///   are self-loops.
    /// - GraphML hyperedges become hyperedges in 2-mode layers.
    /// When exporting, a single layer is written as a GraphML graph with the layer name as graph id. When exporting
    /// several layers, each edge/hyperedge gets a 'layer' attribute, and edges whose directionality differs from the
    /// graph's 'edgedefault' get an explicit 'directed' attribute. Such a file is imported back into the same layers.
    /// </summary>
    internal static class FileSerializerGraphml
    {
        #region Constants
        private const string GraphmlNamespace = "http://graphml.graphdrawing.org/xmlns";
        private const string DefaultEdgeLayerName = "edges";
        private const string DefaultHyperedgeLayerName = "hyperedges";
        private const string HyperedgeNameAttr = "name";
        #endregion


        #region Nested types
        /// <summary>
        /// A GraphML key definition.
        /// </summary>
        private sealed class KeyDef
        {
            internal string Id = "";
            internal string Domain = "all";
            internal string Name = "";
            internal string Type = "string";
            internal string? Default;
            internal bool Skip;
        }

        /// <summary>
        /// A parsed GraphML edge, with endpoints given as node indices.
        /// </summary>
        private struct EdgeRecord
        {
            internal int Source;
            internal int Target;
            internal float Weight;
            internal bool HasWeight;
            internal bool IsDirected;
            internal int LayerIndex;
        }

        /// <summary>
        /// A parsed GraphML hyperedge, with endpoints given as node indices.
        /// </summary>
        private sealed class HyperedgeRecord
        {
            internal string? Name;
            internal List<int> NodeIndices = [];
            internal int LayerIndex;
        }

        /// <summary>
        /// Everything collected while parsing a GraphML file.
        /// </summary>
        private sealed class ParsedGraph
        {
            internal string? GraphId;
            internal bool EdgeDefaultDirected = true;
            internal Dictionary<string, KeyDef> Keys = [];
            internal List<string> NodeIds = [];
            internal Dictionary<string, int> NodeIdToIndex = [];
            internal List<Dictionary<string, string>?> NodeData = [];
            internal List<EdgeRecord> Edges = [];
            internal List<HyperedgeRecord> Hyperedges = [];
            internal List<string?> EdgeLayerNames = [null];
            internal Dictionary<string, int> EdgeLayerIndex = [];
            internal List<string?> HyperedgeLayerNames = [null];
            internal Dictionary<string, int> HyperedgeLayerIndex = [];
        }
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Imports a GraphML file as a new Network (with a new Nodeset).
        /// </summary>
        /// <param name="filepath">The GraphML file.</param>
        /// <param name="layerAttr">Name of the edge/hyperedge attribute whose values give the layer (null or empty: no splitting into layers).</param>
        /// <param name="weightAttr">Name of the edge attribute holding edge values (null or empty: no edge values).</param>
        /// <param name="idAttr">Name of the string node attribute that GraphML node ids are stored in if these are not unsigned integers.</param>
        /// <returns>A <see cref="StructureResult"/> with the Network, and the Nodeset as an additional structure.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found.</exception>
        /// <exception cref="InvalidDataException">Thrown if the file can not be imported.</exception>
        internal static StructureResult Import(string filepath, string? layerAttr, string? weightAttr, string idAttr)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");

            ParsedGraph graph = Parse(filepath, layerAttr, weightAttr);

            string name = Path.GetFileNameWithoutExtension(filepath);
            var nodeset = new Nodeset(name + "_nodeset");
            uint[] nodeIds = CreateNodes(graph, nodeset, idAttr);
            SetNodeAttributes(graph, nodeset, nodeIds);

            var network = new Network(name, nodeset);
            CreateOneModeLayers(graph, network, nodeIds);
            CreateTwoModeLayers(graph, network, nodeIds);

            network.IsModified = true;
            nodeset.IsModified = true;
            return new StructureResult(network, new Dictionary<string, IStructure>
            {
                { "nodeset", nodeset }
            });
        }

        /// <summary>
        /// Exports a network to a GraphML file. If <paramref name="layerName"/> is provided, only that layer is
        /// exported, otherwise all layers are exported. All nodes and node attributes are exported.
        /// </summary>
        /// <param name="network">The network to export.</param>
        /// <param name="layerName">The layer to export (null or empty: all layers).</param>
        /// <param name="filepath">The filepath to write to.</param>
        /// <exception cref="InvalidOperationException">Thrown if the specified layer is not found.</exception>
        internal static void Export(Network network, string? layerName, string filepath)
        {
            List<ILayer> layers;
            if (!string.IsNullOrEmpty(layerName))
            {
                if (!network.Layers.TryGetValue(layerName, out var layer))
                    throw new InvalidOperationException($"No layer named '{layerName}' found.");
                layers = [layer];
            }
            else
                layers = [.. network.Layers.Values];

            var oneModeLayers = layers.OfType<ILayerOneMode>().ToList();
            var twoModeLayers = layers.OfType<ILayerTwoMode>().ToList();
            bool multipleLayers = layers.Count > 1;
            bool edgeDefaultDirected = oneModeLayers.Count == 0 || oneModeLayers.All(l => l.IsDirectional);
            bool anyValued = oneModeLayers.Any(l => l.IsValued);

            Nodeset nodeset = network.Nodeset;
            var attrDefinitions = nodeset.NodeAttributeDefinitionManager.GetAllNodeAttributeDefinitions().OrderBy(a => a.Index).ToList();
            var indexToType = nodeset.NodeAttributeDefinitionManager.IndexToType;

            using var writer = XmlWriter.Create(filepath, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) });
            writer.WriteStartDocument();
            writer.WriteStartElement("graphml", GraphmlNamespace);

            // Keys
            foreach (var attr in attrDefinitions)
                WriteKey(writer, $"v{attr.Index}", "node", attr.AttrName, GraphmlType(attr.AttrType));
            if (anyValued)
                WriteKey(writer, "e_weight", "edge", "weight", "double");
            if (multipleLayers && oneModeLayers.Count > 0)
                WriteKey(writer, "e_layer", "edge", "layer", "string");
            if (twoModeLayers.Count > 0)
                WriteKey(writer, "h_name", "hyperedge", HyperedgeNameAttr, "string");
            if (multipleLayers && twoModeLayers.Count > 0)
                WriteKey(writer, "h_layer", "hyperedge", "layer", "string");

            // Graph
            writer.WriteStartElement("graph");
            writer.WriteAttributeString("id", multipleLayers || layers.Count == 0 ? network.Name : layers[0].Name);
            writer.WriteAttributeString("edgedefault", edgeDefaultDirected ? "directed" : "undirected");

            // Nodes
            foreach (uint nodeId in nodeset.NodeIdArray.OrderBy(id => id))
            {
                writer.WriteStartElement("node");
                writer.WriteAttributeString("id", nodeId.ToString(CultureInfo.InvariantCulture));
                var tuple = nodeset.GetNodeAttributeTuple(nodeId);
                if (tuple != null)
                {
                    var (attrIndexes, attrValues) = tuple.Value;
                    for (int i = 0; i < attrIndexes.Count; i++)
                    {
                        NodeAttributeType attrType = indexToType[attrIndexes[i]];
                        string valueStr = attrType switch
                        {
                            NodeAttributeType.String => nodeset.GetStringFromPool(attrValues[i].RawValueAsInt()),
                            NodeAttributeType.Bool => (bool)attrValues[i].GetValue(attrType)! ? "true" : "false",
                            _ => attrValues[i].ToString(attrType)
                        };
                        WriteData(writer, $"v{attrIndexes[i]}", valueStr);
                    }
                }
                writer.WriteEndElement();
            }

            // Edges
            foreach (var layer in oneModeLayers)
            {
                bool writeDirected = layer.IsDirectional != edgeDefaultDirected;
                foreach (var (egoId, alters, values) in layer.GetAllEgoData())
                {
                    // Note: for symmetric layers, GetAllEgoData() yields each edge once (from the lower node id)
                    ReadOnlySpan<uint> alterSpan = alters.Span;
                    ReadOnlySpan<float> valueSpan = values.Span;
                    for (int i = 0; i < alterSpan.Length; i++)
                        WriteEdge(writer, layer, egoId, alterSpan[i], layer.IsValued ? valueSpan[i] : 1, writeDirected, multipleLayers);
                }
                // GetAllEgoData() never yields self-loops for symmetric layers: add them explicitly
                if (layer.IsSymmetric && layer.Selfties)
                    foreach (uint nodeId in nodeset.NodeIdArray.OrderBy(id => id))
                        if (layer.CheckEdgeExists(nodeId, nodeId))
                            WriteEdge(writer, layer, nodeId, nodeId, layer.GetEdgeValue(nodeId, nodeId), writeDirected, multipleLayers);
            }

            // Hyperedges
            foreach (var layer in twoModeLayers)
            {
                foreach (var (hyperName, hyperNodeIds) in layer.GetAllHyperedgeData())
                {
                    writer.WriteStartElement("hyperedge");
                    WriteData(writer, "h_name", hyperName);
                    if (multipleLayers)
                        WriteData(writer, "h_layer", layer.Name);
                    foreach (uint nodeId in hyperNodeIds)
                    {
                        writer.WriteStartElement("endpoint");
                        writer.WriteAttributeString("node", nodeId.ToString(CultureInfo.InvariantCulture));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement(); // graph
            writer.WriteEndElement(); // graphml
            writer.WriteEndDocument();
        }
        #endregion


        #region Methods (private): parsing
        /// <summary>
        /// Parses a GraphML file (streaming) into a <see cref="ParsedGraph"/>.
        /// </summary>
        private static ParsedGraph Parse(string filepath, string? layerAttr, string? weightAttr)
        {
            var graph = new ParsedGraph();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                IgnoreProcessingInstructions = true
            };
            using var reader = XmlReader.Create(filepath, settings);

            bool graphFound = false;
            string? edgeLayerKey = null, edgeWeightKey = null, hyperLayerKey = null, hyperNameKey = null;
            bool keysResolved = false;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;
                switch (reader.LocalName)
                {
                    case "key":
                        KeyDef key = ReadKey(reader);
                        graph.Keys[key.Id] = key;
                        break;
                    case "graph":
                        if (graphFound)
                            throw new InvalidDataException("GraphML files with several or nested graphs are not supported.");
                        graphFound = true;
                        graph.GraphId = reader.GetAttribute("id");
                        graph.EdgeDefaultDirected = reader.GetAttribute("edgedefault") != "undirected";
                        break;
                    case "node":
                        if (!keysResolved)
                        {
                            ResolveKeys(graph, layerAttr, weightAttr, out edgeLayerKey, out edgeWeightKey, out hyperLayerKey, out hyperNameKey);
                            keysResolved = true;
                        }
                        ReadNode(reader, graph);
                        break;
                    case "edge":
                        if (!keysResolved)
                        {
                            ResolveKeys(graph, layerAttr, weightAttr, out edgeLayerKey, out edgeWeightKey, out hyperLayerKey, out hyperNameKey);
                            keysResolved = true;
                        }
                        ReadEdge(reader, graph, edgeLayerKey, edgeWeightKey);
                        break;
                    case "hyperedge":
                        if (!keysResolved)
                        {
                            ResolveKeys(graph, layerAttr, weightAttr, out edgeLayerKey, out edgeWeightKey, out hyperLayerKey, out hyperNameKey);
                            keysResolved = true;
                        }
                        ReadHyperedge(reader, graph, hyperLayerKey, hyperNameKey);
                        break;
                }
            }
            if (!graphFound)
                throw new InvalidDataException("No <graph> element found: not a valid GraphML file.");
            return graph;
        }

        /// <summary>
        /// Reads a key element (the reader is positioned on it).
        /// </summary>
        private static KeyDef ReadKey(XmlReader reader)
        {
            var key = new KeyDef
            {
                Id = reader.GetAttribute("id") ?? throw new InvalidDataException("GraphML <key> element without id."),
                Domain = reader.GetAttribute("for") ?? "all",
                Type = reader.GetAttribute("attr.type") ?? "string",
                // yFiles-specific keys (graphics etc.) hold XML content rather than values: skip these
                Skip = reader.GetAttribute("yfiles.type") != null
            };
            key.Name = reader.GetAttribute("attr.name") ?? key.Id;
            if (!reader.IsEmptyElement)
            {
                using var sub = reader.ReadSubtree();
                sub.Read();
                while (sub.Read())
                    if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "default")
                        key.Default = sub.ReadElementContentAsString();
            }
            return key;
        }

        /// <summary>
        /// Finds the keys for edge layer, edge weight, hyperedge layer and hyperedge name.
        /// </summary>
        private static void ResolveKeys(ParsedGraph graph, string? layerAttr, string? weightAttr, out string? edgeLayerKey, out string? edgeWeightKey, out string? hyperLayerKey, out string? hyperNameKey)
        {
            edgeLayerKey = FindKey(graph, "edge", layerAttr);
            edgeWeightKey = FindKey(graph, "edge", weightAttr);
            hyperLayerKey = FindKey(graph, "hyperedge", layerAttr);
            hyperNameKey = FindKey(graph, "hyperedge", HyperedgeNameAttr);
        }

        /// <summary>
        /// Finds the id of the key with the given attribute name that applies to the given domain.
        /// </summary>
        private static string? FindKey(ParsedGraph graph, string domain, string? attrName)
        {
            if (string.IsNullOrEmpty(attrName))
                return null;
            foreach (var key in graph.Keys.Values)
                if (!key.Skip && key.Name == attrName && (key.Domain == domain || key.Domain == "all"))
                    return key.Id;
            return null;
        }

        /// <summary>
        /// Reads the data elements of the current element (node, edge or hyperedge), also collecting
        /// endpoint references for hyperedges. Data for unknown or skipped keys is ignored.
        /// </summary>
        private static Dictionary<string, string>? ReadData(XmlReader reader, ParsedGraph graph, List<string>? endpoints = null)
        {
            if (reader.IsEmptyElement)
                return null;
            Dictionary<string, string>? data = null;
            using var sub = reader.ReadSubtree();
            sub.Read();
            sub.Read();
            while (!sub.EOF)
            {
                if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "data" && sub.Depth == 1)
                {
                    string? keyId = sub.GetAttribute("key");
                    if (keyId != null && graph.Keys.TryGetValue(keyId, out var key) && !key.Skip)
                    {
                        data ??= [];
                        data[keyId] = sub.ReadElementContentAsString();
                        continue;
                    }
                    sub.Skip();
                    continue;
                }
                if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "endpoint" && sub.Depth == 1 && endpoints != null)
                {
                    if (sub.GetAttribute("node") is string endpointNode)
                        endpoints.Add(endpointNode);
                    sub.Skip();
                    continue;
                }
                if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "graph")
                    throw new InvalidDataException("GraphML files with nested graphs are not supported.");
                if (sub.NodeType == XmlNodeType.Element && sub.Depth == 1)
                {
                    sub.Skip();
                    continue;
                }
                sub.Read();
            }
            return data;
        }

        /// <summary>
        /// Reads a node element.
        /// </summary>
        private static void ReadNode(XmlReader reader, ParsedGraph graph)
        {
            string id = reader.GetAttribute("id") ?? throw new InvalidDataException("GraphML <node> element without id.");
            var data = ReadData(reader, graph);
            if (graph.NodeIdToIndex.TryGetValue(id, out int index))
            {
                // Node already referenced by an earlier edge: set its data
                graph.NodeData[index] = data;
                return;
            }
            graph.NodeIdToIndex[id] = graph.NodeIds.Count;
            graph.NodeIds.Add(id);
            graph.NodeData.Add(data);
        }

        /// <summary>
        /// Returns the node index of a GraphML node id, adding the node if it has not been seen before.
        /// </summary>
        private static int GetOrAddNode(ParsedGraph graph, string id)
        {
            if (graph.NodeIdToIndex.TryGetValue(id, out int index))
                return index;
            index = graph.NodeIds.Count;
            graph.NodeIdToIndex[id] = index;
            graph.NodeIds.Add(id);
            graph.NodeData.Add(null);
            return index;
        }

        /// <summary>
        /// Returns the value of a key for an element: either from its data, or the default of the key.
        /// </summary>
        private static string? GetValue(ParsedGraph graph, Dictionary<string, string>? data, string? keyId)
        {
            if (keyId == null)
                return null;
            if (data != null && data.TryGetValue(keyId, out var value))
                return value;
            return graph.Keys[keyId].Default;
        }

        /// <summary>
        /// Returns the layer index for the given layer name (null for the default layer), adding it if new.
        /// </summary>
        private static int GetOrAddLayer(List<string?> layerNames, Dictionary<string, int> layerIndex, string? layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return 0;
            layerName = layerName.Trim();
            if (!layerIndex.TryGetValue(layerName, out int index))
            {
                index = layerNames.Count;
                layerNames.Add(layerName);
                layerIndex[layerName] = index;
            }
            return index;
        }

        /// <summary>
        /// Reads an edge element.
        /// </summary>
        private static void ReadEdge(XmlReader reader, ParsedGraph graph, string? layerKey, string? weightKey)
        {
            string source = reader.GetAttribute("source") ?? throw new InvalidDataException("GraphML <edge> element without source.");
            string target = reader.GetAttribute("target") ?? throw new InvalidDataException("GraphML <edge> element without target.");
            string? directedStr = reader.GetAttribute("directed");
            var data = ReadData(reader, graph);

            var edge = new EdgeRecord
            {
                Source = GetOrAddNode(graph, source),
                Target = GetOrAddNode(graph, target),
                Weight = 1,
                IsDirected = directedStr == null ? graph.EdgeDefaultDirected : directedStr == "true" || directedStr == "1",
                LayerIndex = GetOrAddLayer(graph.EdgeLayerNames, graph.EdgeLayerIndex, GetValue(graph, data, layerKey))
            };
            if (GetValue(graph, data, weightKey) is string weightStr && float.TryParse(weightStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float weight))
            {
                edge.Weight = weight;
                edge.HasWeight = true;
            }
            graph.Edges.Add(edge);
        }

        /// <summary>
        /// Reads a hyperedge element.
        /// </summary>
        private static void ReadHyperedge(XmlReader reader, ParsedGraph graph, string? layerKey, string? nameKey)
        {
            string? id = reader.GetAttribute("id");
            var endpoints = new List<string>();
            var data = ReadData(reader, graph, endpoints);
            var hyperedge = new HyperedgeRecord
            {
                Name = GetValue(graph, data, nameKey) ?? id,
                LayerIndex = GetOrAddLayer(graph.HyperedgeLayerNames, graph.HyperedgeLayerIndex, GetValue(graph, data, layerKey))
            };
            foreach (string endpoint in endpoints)
                hyperedge.NodeIndices.Add(GetOrAddNode(graph, endpoint));
            graph.Hyperedges.Add(hyperedge);
        }
        #endregion


        #region Methods (private): building the network
        /// <summary>
        /// Creates the nodes in the Nodeset. Returns the Threadle node id for each node index.
        /// </summary>
        private static uint[] CreateNodes(ParsedGraph graph, Nodeset nodeset, string idAttr)
        {
            int nbrNodes = graph.NodeIds.Count;
            uint[] nodeIds = new uint[nbrNodes];
            bool numericIds = true;
            for (int i = 0; i < nbrNodes && numericIds; i++)
                numericIds = uint.TryParse(graph.NodeIds[i], NumberStyles.None, CultureInfo.InvariantCulture, out nodeIds[i]) && nodeIds[i].ToString(CultureInfo.InvariantCulture) == graph.NodeIds[i];

            if (numericIds)
            {
                foreach (uint nodeId in nodeIds)
                    nodeset._addNodeWithoutAttribute(nodeId);
                return nodeIds;
            }

            // Non-numeric ids: create new node ids, storing GraphML ids in a string attribute
            if (graph.Keys.Values.Any(k => !k.Skip && k.Name == idAttr && (k.Domain == "node" || k.Domain == "all")))
                throw new InvalidDataException($"The GraphML node ids are not unsigned integers and must be stored in a node attribute, but the file already has a node attribute named '{idAttr}': choose another attribute name for the node ids.");
            var defineResult = nodeset.DefineNodeAttribute(idAttr, NodeAttributeType.String);
            if (!defineResult.Success)
                throw new InvalidDataException(defineResult.Message);
            byte attrIndex = defineResult.Value;
            for (int i = 0; i < nbrNodes; i++)
            {
                nodeIds[i] = (uint)i;
                nodeset._addNodeWithoutAttribute(nodeIds[i]);
                nodeset.SetNodeAttribute(nodeIds[i], attrIndex, new NodeAttributeValue(nodeset.GetOrAddStringToPool(graph.NodeIds[i])));
            }
            return nodeIds;
        }

        /// <summary>
        /// Defines node attributes for all node keys, and sets their values (or key defaults) for all nodes.
        /// </summary>
        private static void SetNodeAttributes(ParsedGraph graph, Nodeset nodeset, uint[] nodeIds)
        {
            foreach (var key in graph.Keys.Values)
            {
                if (key.Skip || (key.Domain != "node" && key.Domain != "all"))
                    continue;
                NodeAttributeType attrType = ThreadleType(key.Type);
                var defineResult = nodeset.DefineNodeAttribute(key.Name, attrType);
                if (!defineResult.Success)
                    continue;
                byte attrIndex = defineResult.Value;
                for (int i = 0; i < nodeIds.Length; i++)
                {
                    if (GetValue(graph, graph.NodeData[i], key.Id) is not string valueStr)
                        continue;
                    if (ParseValue(nodeset, attrType, valueStr) is NodeAttributeValue value)
                        nodeset.SetNodeAttribute(nodeIds[i], attrIndex, value);
                }
            }
        }

        /// <summary>
        /// Creates the 1-mode layers and adds their edges.
        /// </summary>
        private static void CreateOneModeLayers(ParsedGraph graph, Network network, uint[] nodeIds)
        {
            int nbrLayers = graph.EdgeLayerNames.Count;
            int[] nbrEdges = new int[nbrLayers], nbrDirected = new int[nbrLayers];
            bool[] valued = new bool[nbrLayers], selfties = new bool[nbrLayers];
            foreach (var edge in graph.Edges)
            {
                nbrEdges[edge.LayerIndex]++;
                if (edge.IsDirected)
                    nbrDirected[edge.LayerIndex]++;
                valued[edge.LayerIndex] |= edge.HasWeight;
                selfties[edge.LayerIndex] |= edge.Source == edge.Target;
            }

            var layers = new LayerOneMode?[nbrLayers];
            for (int l = 0; l < nbrLayers; l++)
            {
                // The default layer (edges without layer attribute) is only created if it has edges
                if (l == 0 && nbrEdges[0] == 0)
                    continue;
                string layerName = graph.EdgeLayerNames[l] ?? graph.GraphId ?? DefaultEdgeLayerName;
                if (string.IsNullOrWhiteSpace(layerName))
                    layerName = DefaultEdgeLayerName;
                // A layer is directed if any of its edges is directed (undirected edges are then added in both directions)
                bool directed = nbrEdges[l] == 0 ? graph.EdgeDefaultDirected : nbrDirected[l] > 0;
                var layer = new LayerOneMode(layerName, directed ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected, valued[l] ? EdgeType.Valued : EdgeType.Binary, selfties[l]);
                var result = network.AddLayer(layerName, layer);
                if (!result.Success)
                    throw new InvalidDataException(result.Message);
                layers[l] = layer;
            }

            foreach (var edge in graph.Edges)
            {
                var layer = layers[edge.LayerIndex]!;
                uint source = nodeIds[edge.Source], target = nodeIds[edge.Target];
                layer._addEdge(source, target, edge.Weight);
                if (layer.IsDirectional && !edge.IsDirected && source != target)
                    layer._addEdge(target, source, edge.Weight);
            }
            foreach (var layer in layers)
                layer?._deduplicateEdgesets();
        }

        /// <summary>
        /// Creates the 2-mode layers and adds their hyperedges.
        /// </summary>
        private static void CreateTwoModeLayers(ParsedGraph graph, Network network, uint[] nodeIds)
        {
            int nbrLayers = graph.HyperedgeLayerNames.Count;
            var layers = new LayerTwoMode?[nbrLayers];
            int hyperedgeCounter = 0;
            foreach (var hyperedge in graph.Hyperedges)
            {
                int l = hyperedge.LayerIndex;
                if (layers[l] == null)
                {
                    string layerName = graph.HyperedgeLayerNames[l]
                        ?? (graph.Edges.Count == 0 && !string.IsNullOrWhiteSpace(graph.GraphId) ? graph.GraphId : DefaultHyperedgeLayerName);
                    var layer = new LayerTwoMode(layerName);
                    var result = network.AddLayer(layerName, layer);
                    if (!result.Success)
                        throw new InvalidDataException(result.Message);
                    layers[l] = layer;
                }
                string name = string.IsNullOrWhiteSpace(hyperedge.Name) ? $"h{hyperedgeCounter}" : hyperedge.Name.Trim();
                hyperedgeCounter++;
                if (!Misc.IsNameWithinBinaryLimit(name))
                    throw new InvalidDataException($"Hyperedge name too long: {name}");
                foreach (int nodeIndex in hyperedge.NodeIndices)
                    layers[l]!._addAffiliation(nodeIds[nodeIndex], name);
            }
        }
        #endregion


        #region Methods (private): helpers
        /// <summary>
        /// Maps a GraphML attribute type to a Threadle node attribute type.
        /// </summary>
        private static NodeAttributeType ThreadleType(string graphmlType) => graphmlType switch
        {
            "int" or "long" => NodeAttributeType.Int,
            "float" or "double" => NodeAttributeType.Float,
            "boolean" => NodeAttributeType.Bool,
            _ => NodeAttributeType.String
        };

        /// <summary>
        /// Maps a Threadle node attribute type to a GraphML attribute type.
        /// </summary>
        private static string GraphmlType(NodeAttributeType attrType) => attrType switch
        {
            NodeAttributeType.Int => "int",
            NodeAttributeType.Float => "double",
            NodeAttributeType.Bool => "boolean",
            _ => "string"
        };

        /// <summary>
        /// Parses a GraphML value string into a node attribute value of the given type. Returns null if not parseable.
        /// </summary>
        private static NodeAttributeValue? ParseValue(Nodeset nodeset, NodeAttributeType attrType, string valueStr)
        {
            valueStr = valueStr.Trim();
            switch (attrType)
            {
                case NodeAttributeType.Int:
                    return int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? new NodeAttributeValue(i) : null;
                case NodeAttributeType.Float:
                    return float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? new NodeAttributeValue(f) : null;
                case NodeAttributeType.Bool:
                    if (valueStr.Equals("true", StringComparison.OrdinalIgnoreCase) || valueStr == "1")
                        return new NodeAttributeValue(true);
                    if (valueStr.Equals("false", StringComparison.OrdinalIgnoreCase) || valueStr == "0")
                        return new NodeAttributeValue(false);
                    return null;
                default:
                    return new NodeAttributeValue(nodeset.GetOrAddStringToPool(valueStr));
            }
        }

        /// <summary>
        /// Writes an edge element.
        /// </summary>
        private static void WriteEdge(XmlWriter writer, ILayerOneMode layer, uint source, uint target, float value, bool writeDirected, bool writeLayer)
        {
            writer.WriteStartElement("edge");
            writer.WriteAttributeString("source", source.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("target", target.ToString(CultureInfo.InvariantCulture));
            if (writeDirected)
                writer.WriteAttributeString("directed", layer.IsDirectional ? "true" : "false");
            if (layer.IsValued)
                WriteData(writer, "e_weight", value.ToString("R", CultureInfo.InvariantCulture));
            if (writeLayer)
                WriteData(writer, "e_layer", layer.Name);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Writes a key element.
        /// </summary>
        private static void WriteKey(XmlWriter writer, string id, string domain, string name, string type)
        {
            writer.WriteStartElement("key");
            writer.WriteAttributeString("id", id);
            writer.WriteAttributeString("for", domain);
            writer.WriteAttributeString("attr.name", name);
            writer.WriteAttributeString("attr.type", type);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Writes a data element.
        /// </summary>
        private static void WriteData(XmlWriter writer, string key, string value)
        {
            writer.WriteStartElement("data");
            writer.WriteAttributeString("key", key);
            writer.WriteString(value);
            writer.WriteEndElement();
        }
        #endregion
    }
}
