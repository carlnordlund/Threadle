using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes a Network object, consisting of a Nodeset object and a set of layers.
    /// Implements IStructure.
    /// Constructors with or without provided Nodeset.
    /// </summary>
    public class Network : IStructure
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="Network"/> class with the specified
        /// name and Nodeset.
        /// </summary>
        /// <param name="name">The internal name of the network.</param>
        /// <param name="nodeset">The Nodeset object that the network is using.</param>
        public Network(string name, Nodeset nodeset)
        {
            Name = name;
            Nodeset = nodeset;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Network"/> class
        /// with the specified name. Also creating a nodeset. Used by TSV loader.
        /// </summary>
        /// <param name="name">The internal name of the network.</param>
        public Network(string name)
        {
            Name = name;
            Nodeset = new Nodeset(name + "_nodeset");
        }
        #endregion


        #region Properties
        /// <summary>
        /// Gets or sets the name of the Network.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Returns content info about this structure as a list of strings
        /// </summary>
        public List<string> Content
        {
            get
            {
                List<string> lines = [$"Network: {Name}", $"Nodeset: {Nodeset.Name}"];
                foreach ((string layerName, ILayer layer) in Layers)
                {
                    if (layer is LayerOneMode layerOneMode)
                        lines.Add($" {layerOneMode.Name} (1-mode: {layerOneMode.EdgeValueType},{layerOneMode.Directionality},{layerOneMode.Selfties}); Nbr edges:{layerOneMode.NbrEdges}");
                    else if (layer is LayerTwoMode layerTwoMode)
                    {
                        lines.Add($" {layerTwoMode.Name} (2-mode); Nbr hyperedges: {layerTwoMode.NbrHyperedges}");
                    }
                }
                return lines;
            }
        }

        /// <summary>
        /// Gets or sets the Filename (if this structure is loaded or saved to file)
        /// </summary>
        public string Filepath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the flag whether this structure has been modified or not since last load/initiation
        /// </summary>
        public bool IsModified { get; set; }

        /// <summary>
        /// Returns a dictionary with meta-information about this Network, including nodeset name and Layers.
        /// </summary>
        public Dictionary<string, object> Info => new Dictionary<string, object>
            {
                ["Type"] = "Network",
                ["Name"] = Name,
                ["Filepath"] = Filepath,
                ["isModified"] = IsModified,
                ["Nodeset"] = Nodeset.Name,
                ["Layers"] = Layers.Select(kvp => kvp.Value.GetMetadata).ToList()
            };

        /// <summary>
        /// Gets the Nodeset that this Network uses
        /// </summary>
        public Nodeset Nodeset { get; private set; }

        /// <summary>
        /// A dictionary of relational layers (ILayer), accessible by their unique names.
        /// </summary>
        public Dictionary<string, ILayer> Layers { get; set; } = [];
        #endregion


        #region Methods (public)
        /// <summary>
        /// Creates a layer of 1-mode relations (see <see cref="LayerOneMode"/>) with the specified name and relational properties.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="edgeDirectionality">Either directed (<see cref="EdgeDirectionality.Directed"/>) or undirected (<see cref="EdgeDirectionality.Undirected">).</param>
        /// <param name="edgeValueType">Either binary (<see cref="EdgeType.Binary"/>), valued (<see cref="EdgeType.Valued"/>), or signed (<see cref="EdgeType.Signed"/>).</param>
        /// <param name="selfties">Allow selfties (true) or not (false)</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddLayerOneMode(string layerName, EdgeDirectionality edgeDirectionality, EdgeType edgeValueType, bool selfties)
        {
            return AddLayer(layerName, new LayerOneMode(layerName, edgeDirectionality, edgeValueType, selfties));
        }

        /// <summary>
        /// Creates a layer of 2-mode hyperdge relations (see <see cref="LayerTwoMode"/>) with the specified name.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddLayerTwoMode(string layerName)
        {
            return AddLayer(layerName, new LayerTwoMode(layerName));
        }

        /// <summary>
        /// Removes a layer with the specific layername. Also clears out all edges in that layer.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveLayer(string layerName)
        {
            if (!(GetLayer(layerName) is ILayer layer))
                return OperationResult.Fail("LayerNotFound", $"No layer with name '{layerName}' found.");

            //var layerResult = GetLayer(layerName);
            //if (!layerResult.Success)
            //    return layerResult;
            //layerResult.Value!.ClearLayer();
            layer.ClearLayer();
            Layers.Remove(layerName);
            IsModified = true;
            return OperationResult.Ok($"Layer '{layerName}' removed from network '{Name}'.");
        }

        /// <summary>
        /// Clears a layer, i.e. removing all edges from that layer
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult ClearLayer(string layerName)
        {
            var layerResult = GetLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            layerResult.Value!.ClearLayer();
            IsModified = true;
            return OperationResult.Ok($"All edges removed from layer '{layerName}' in network '{Name}'.");
        }

        /// <summary>
        /// Generates the next available layer name based on the specified base name.
        /// </summary>
        /// <param name="baseName">The base name to use for generating the layer name. Defaults to <c>"layer-"</c> if not specified.</param>
        /// <returns>A unique layer name that does not already exist in the collection of layers.</returns>
        public string GetNextAvailableLayerName(string baseName = "layer")
        {
            if (!Layers.ContainsKey(baseName))
                return baseName;
            int counter = 1;
            string layerName;
            do
            {
                layerName = $"{baseName}-{counter}";
                counter++;
            } while (Layers.ContainsKey(layerName));
            return layerName;
        }

        /// <summary>
        /// Adds an edge between <paramref name="node1id"/> and <paramref name="node2id"/>, in the specified (1-mode) layer.
        /// The edge is either directional or symmetric depending on the properties of the layer.
        /// The default edge value is 1, but this can be set for valued layers.
        /// An optional flag allows for creating and adding nodes to the nodeset in case they are missing.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="node1id">Id of the first node.</param>
        /// <param name="node2id">Id of the second node.</param>
        /// <param name="value">Value of the edge.</param>
        /// <param name="addMissingNodes">Indicates whether non-existing nodes should be added.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddEdge(string layerName, uint node1id, uint node2id, float value = 1, bool addMissingNodes = false)
        {
            var layerResult = GetOneModeLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            return AddEdge(layerResult.Value!, node1id, node2id, value, addMissingNodes);
        }

        /// <summary>
        /// Removes an (the) edge between node1id and node2id in the specified 1-mode layer.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="node1id">The first node id.</param>
        /// <param name="node2id">The second node id.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveEdge(string layerName, uint node1id, uint node2id)
        {
            var layerResult = GetOneModeLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            return RemoveEdge(layerResult.Value!, node1id, node2id);
        }

        /// <summary>
        /// Removes all edges for a node id in all layers.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        public void RemoveNodeEdges(uint nodeId)
        {
            foreach (ILayer layer in Layers.Values)
                layer.RemoveNodeEdges(nodeId);
        }

        /// <summary>
        /// Adds an hyperedge in the specified (2-mode) layer. An optional array with node ids indicates the nodes that are connected
        /// to this hyperedge. An optional flag allows for creating and adding nodes to the nodeset in case they are missing.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="hyperName">The (unique) name of the hyperedge.</param>
        /// <param name="nodeIds">An array of node ids (uint[]).</param>
        /// <param name="addMissingNodes">Indicates whether non-existing nodes should be added.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddHyperedge(string layerName, string hyperName, uint[]? nodeIds = null, bool addMissingNodes = true)
        {
            var layerResult = GetTwoModeLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            return AddHyperedge(layerResult.Value!, hyperName, nodeIds, addMissingNodes);
        }

        /// <summary>
        /// Removes the specified Hyperedge and its connections to nodes in the specificed layer.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="hyperName">The name of the Hyperedge.</param>
        /// <returns></returns>
        public OperationResult RemoveHyperedge(string layerName, string hyperName)
        {
            var layerResult = GetTwoModeLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            return RemoveHyperedge(layerResult.Value!, hyperName);
        }

        /// <summary>
        /// Adds an node affiliation in the specified (2-mode) layer. Optional flags allow for creating and adding nodes and hyperedges to the nodeset
        /// in case they are missing.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="hyperName">The (unique) name of the hyperedge.</param>
        /// <param name="nodeId">The node id.</param>
        /// <param name="addMissingNode">Indicates whether a non-existing node should be added.</param>
        /// <param name="addMissingHyperedge">Indicates whether a non-existing hyperedge should be added.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddAffiliation(string layerName, string hyperName, uint nodeId, bool addMissingNode, bool addMissingHyperedge)
        {
            var layerResult = GetTwoModeLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            return AddAffiliation(layerResult.Value!, hyperName, nodeId, addMissingNode, addMissingHyperedge);
        }

        public OperationResult RemoveAffiliation(string layerName, string hyperName, uint nodeId)
        {
            var layerResult = GetTwoModeLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            return RemoveAffiliation(layerResult.Value!, hyperName, nodeId);

        }


        /// <summary>
        /// Check if an edge exists between two nodes in a particular layer. Works for both 1- and 2-mode layers.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="node1id">Id of the first node.</param>
        /// <param name="node2id">Id of the second node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="bool"/>.</returns>
        public OperationResult<bool> CheckEdgeExists(string layerName, uint node1id, uint node2id)
        {
            OperationResult nodeCheckResult = Nodeset.CheckThatNodesExist(node1id, node2id);
            if (!nodeCheckResult.Success)
                return OperationResult<bool>.Fail(nodeCheckResult.Code, nodeCheckResult.Message);
            var layerResult = GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<bool>.Fail(layerResult.Code, layerResult.Message);
            return OperationResult<bool>.Ok(layerResult.Value!.CheckEdgeExists(node1id, node2id));
        }

        /// <summary>
        /// Gets the potential edge value between two nodes in a layer. If the check is valid, a zero
        /// value is returned if there is no edge between the nodes.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="node1id">Id of the first node.</param>
        /// <param name="node2id">Id of the second node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="float"/>.</returns>
        public OperationResult<float> GetEdge(string layerName, uint node1id, uint node2id)
        {
            OperationResult nodeCheckResult = Nodeset.CheckThatNodesExist(node1id, node2id);
            if (!nodeCheckResult.Success)
                return OperationResult<float>.Fail(nodeCheckResult.Code, nodeCheckResult.Message);
            var layerResult = GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<float>.Fail(layerResult.Code, layerResult.Message);
            return OperationResult<float>.Ok(layerResult.Value!.GetEdgeValue(node1id, node2id));
        }

        /// <summary>
        /// Returns an array of node ids for the alter of a specified ego node in a specified layer.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="nodeId">The node id.</param>
        /// <param name="edgeTraversal">A <see cref="EdgeTraversal"/> value indicating which alters should be included.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="uint"/> array.</returns>
        public OperationResult<uint[]> GetNodeAlters(string layerName, uint nodeId, EdgeTraversal edgeTraversal = EdgeTraversal.Both)
        {
            if (!Nodeset.CheckThatNodeExists(nodeId))
                return OperationResult<uint[]>.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            var layerResult = GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<uint[]>.Fail(layerResult.Code, layerResult.Message);
            uint[] alterIds = Nodeset.FilterOutNonExistingNodeIds(layerResult.Value!.GetAlterIds(nodeId, edgeTraversal));
            Array.Sort(alterIds);
            return OperationResult<uint[]>.Ok(alterIds);
        }
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Creates a layer of relations (see <see cref="ILayer"/>) with the specified name.
        /// </summary>
        /// <param name="layerName">The name of the layer<./param>
        /// <param name="layer">The <see cref="ILayer"/> object.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult AddLayer(string layerName, ILayer layer)
        {
            layerName = layerName.Trim();
            if (string.IsNullOrEmpty(layerName))
                return OperationResult.Fail("InvalidLayerName", "Layer name cannot be empty.");
            var layerResult = GetLayer(layerName);
            if (layerResult.Success)
                return OperationResult.Fail("LayerAlreadyExists", $"Layer with name '{layerName}' already exists.");
            Layers[layerName] = layer;
            IsModified = true;
            return OperationResult.Ok($"Layer '{layerName}' added to network '{Name}'");
        }

        /// <summary>
        /// Gets the <see cref="ILayer"/> object for the specified layer, packaged in an OperationResult object.
        /// Can be either a 1-mode or a 2-mode layer.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="ILayer"/>.</returns>
        internal OperationResult<ILayer> GetLayer(string layerName)
        {
            if (!Layers.TryGetValue(layerName, out var layer))
                return OperationResult<ILayer>.Fail("LayerNotFound", $"No layer with name '{layerName}' found.");
            return OperationResult<ILayer>.Ok(layer);
        }

        /// <summary>
        /// Gets the <see cref="LayerOneMode"/> object for the specifed layer.
        /// </summary>
        /// <param name="layerName">The name of the 1-mode layer.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="LayerOneMode"/>.</returns>
        internal OperationResult<LayerOneMode> GetOneModeLayer(string layerName)
        {
            if (!Layers.TryGetValue(layerName, out var layer))
                return OperationResult<LayerOneMode>.Fail("LayerNotFound", $"No layer with name '{layerName}' found.");
            if (!(layer is LayerOneMode layerOneMode))
                return OperationResult<LayerOneMode>.Fail("LayerNotOneMode", $"Layer '{layerName}' is not a 1-mode layer.");
            return OperationResult<LayerOneMode>.Ok(layerOneMode);
        }

        /// <summary>
        /// Gets the <see cref="LayerTwoMode"/> object for the specifed layer.
        /// </summary>
        /// <param name="layerName">The name of the 2-mode layer.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="LayerTwoMode"/>.</returns>
        internal OperationResult<LayerTwoMode> GetTwoModeLayer(string layerName)
        {
            if (!Layers.TryGetValue(layerName, out var layer))
                return OperationResult<LayerTwoMode>.Fail("LayerNotFound", $"No layer with name '{layerName}' found.");
            if (!(layer is LayerTwoMode layerTwoMode))
                return OperationResult<LayerTwoMode>.Fail("LayerNotTwoMode", $"Layer '{layerName}' is not a 2-mode layer.");
            return OperationResult<LayerTwoMode>.Ok(layerTwoMode);
        }

        /// <summary>
        /// Adds an edge between node1id and node2id in the specified 1-mode layer.
        /// </summary>
        /// <param name="layerOneMode">The <see cref="LayerOneMode"/> layer.</param>
        /// <param name="node1id">Id of the first node.</param>
        /// <param name="node2id">Id of the second node.</param>
        /// <param name="value">Value of the edge.</param>
        /// <param name="addMissingNodes">Indicates whether non-existing nodes should be added.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult AddEdge(LayerOneMode layerOneMode, uint node1id, uint node2id, float value, bool addMissingNodes)
        {
            value = Misc.FixConnectionValue(value, layerOneMode.EdgeValueType);
            if (value == 0)
                return OperationResult.Ok("Edge value is zero: no edge added.");
            if (!addMissingNodes)
            {
                var nodeCheckResult = Nodeset.CheckThatNodesExist(node1id, node2id);
                if (!nodeCheckResult.Success)
                    return nodeCheckResult;
            }
            OperationResult result = layerOneMode.AddEdge(node1id, node2id, value);
            if (result.Success)
            {
                if (addMissingNodes)
                {
                    if (!Nodeset.CheckThatNodeExists(node1id))
                        Nodeset.AddNode(node1id);
                    if (!Nodeset.CheckThatNodeExists(node2id))
                        Nodeset.AddNode(node2id);
                }
                IsModified = true;
            }
            return result;
        }

        /// <summary>
        /// Removes an edge between two nodes in the specified 1-mode layer object.
        /// </summary>
        /// <param name="layerOneMode">The <see cref="LayerOneMode"/> object.</param>
        /// <param name="node1id">The first node id.</param>
        /// <param name="node2id">The second node id.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult RemoveEdge(LayerOneMode layerOneMode, uint node1id, uint node2id)
        {
            var nodeCheckResult = Nodeset.CheckThatNodesExist(node1id, node2id);
            if (!nodeCheckResult.Success)
                return nodeCheckResult;
            OperationResult result = layerOneMode.RemoveEdge(node1id, node2id);
            if (result.Success)
                IsModified = true;
            return result;
        }

        /// <summary>
        /// Adds an hyperedge to the specified 2-mode layer. If provided with array of node ids to add to
        /// this hyperedge, validates that these exist in the Nodeset and that there are no duplicates.
        /// </summary>
        /// <param name="layerTwoMode">The <see cref="LayerTwoMode"/> layer.</param>
        /// <param name="hyperName">The (unique) name of the hyperedge.</param>
        /// <param name="nodeIds">An array of node ids (uint[]).</param>
        /// <param name="addMissingNodes">Indicates whether non-existing nodes should be added.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult AddHyperedge(LayerTwoMode layerTwoMode, string hyperName, uint[]? nodeIds, bool addMissingNodes)
        {
            if (nodeIds != null && nodeIds.Length > 0)
            {
                List<uint> existingNodeIds = [];
                foreach (uint id in nodeIds)
                {
                    if (!Nodeset.CheckThatNodeExists(id))
                    {
                        if (addMissingNodes)
                            Nodeset.AddNode(id);
                        else
                            continue;
                    }
                    existingNodeIds.Add(id);
                }
                // Filter out any duplicates that might exist in the list.
                Misc.DeduplicateUintList(existingNodeIds);
                nodeIds = existingNodeIds.ToArray();
            }
            return layerTwoMode.AddHyperedge(hyperName, nodeIds);
        }

        /// <summary>
        /// Removes a specified hyperedge from the specified 2-mode layer object.
        /// </summary>
        /// <param name="layerTwoMode">The <see cref="LayerTwoMode"/> object.</param>
        /// <param name="hypername">The name of the hyperedge.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult RemoveHyperedge(LayerTwoMode layerTwoMode, string hypername)
        {
            OperationResult result = layerTwoMode.RemoveHyperedge(hypername);
            if (result.Success)
                IsModified = true;
            return result;
        }

        /// <summary>
        /// Adds an node affiliation in the specified (2-mode) layer. Flags allow for creating and adding nodes and hyperedges to the nodeset
        /// in case they are missing.
        /// </summary>
        /// <param name="layerTwoMode">The <see cref="LayerTwoMode"/> object.</param>
        /// <param name="hyperName">The name of the hyperedge.</param>
        /// <param name="nodeId">The node id.</param>
        /// <param name="addMissingNode">Indicates whether a non-existing node should be added.</param>
        /// <param name="addMissingHyperedge">Indicates whether a non-existing hyperedge should be added.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult AddAffiliation(LayerTwoMode layerTwoMode, string hyperName, uint nodeId, bool addMissingNode, bool addMissingHyperedge)
        {
            if (!addMissingNode)
            {
                if (!Nodeset.CheckThatNodeExists(nodeId))
                    return OperationResult.Fail("NodeNotFound", $"Node '{nodeId}' not found in nodeset '{Nodeset.Name}'.");
            }
            if (!addMissingHyperedge)
            {
                if (!layerTwoMode.CheckThatHyperedgeExists(hyperName))
                    return OperationResult.Fail("HyperedgeNotFound", $"Hyperedge '{hyperName}' not found in 2-mode layer '{layerTwoMode.Name}'.");
            }
            OperationResult result = layerTwoMode.AddAffiliation(nodeId, hyperName, addMissingHyperedge);
            if (result.Success)
            {
                if (addMissingNode && !Nodeset.CheckThatNodeExists(nodeId))
                    Nodeset.AddNode(nodeId);
                IsModified = true;
            }
            return result;
        }

        internal OperationResult RemoveAffiliation(LayerTwoMode layerTwoMode, string hyperName, uint nodeId)
        {
            if (!Nodeset.CheckThatNodeExists(nodeId))
                return OperationResult.Fail("NodeNotFound", $"Node '{nodeId}' not found in nodeset '{Nodeset.Name}'.");
            if (!layerTwoMode.CheckThatHyperedgeExists(hyperName))
                return OperationResult.Fail("HyperedgeNotFound", $"Hyperedge '{hyperName}' not found in 2-mode layer '{layerTwoMode.Name}'.");
            OperationResult result = layerTwoMode.RemoveAffiliation(nodeId, hyperName);
            if (result.Success)
                IsModified = true;
            return result;
        }



        /// <summary>
        /// Internal method for setting the Nodeset (only to be used by the loader)
        /// </summary>
        /// <param name="nodeset">The nodeset to set.</param>
        internal void SetNodeset(Nodeset nodeset) => Nodeset = nodeset;

        /// <summary>
        /// Gets a collection of all unique node id values found in all existing relations in all <see cref="Layers"/>.
        /// </summary>
        /// <returns>A HashSet with all unique node ids currently existing in the <see cref="Network">.</returns>
        /// <remarks>
        /// This is currently used by FileSerializerTsv when loading a network without an existing Nodeset. I might change that so that a network MUST have a reference to a saved
        /// Nodeset. Or that it creates Nodes on the fly.
        /// </remarks>
        internal HashSet<uint> GetAllIdsMentioned()
        {
            HashSet<uint> ids = [];
            foreach (ILayer layer in Layers.Values)
                ids.UnionWith(layer.GetMentionedNodeIds());
            return ids;
        }
        #endregion
    }
}
