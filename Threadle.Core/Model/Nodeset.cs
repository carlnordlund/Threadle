using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes a Nodeset, consisting of a set of node id, a node attribute definition manager,
    /// and node attributes.
    /// </summary>
    public class Nodeset : IStructure
    {
        #region Fields
        /// <summary>
        /// Storage container for nodes WITH attributes
        /// </summary>
        private Dictionary<uint, (List<byte> AttrIndexes, List<NodeAttributeValue> AttrValues)> _nodesWithAttributes = new();

        /// <summary>
        /// Storage container for nodes WITHOUT attributes
        /// </summary>
        private HashSet<uint> _nodesWithoutAttributes = new();

        /// <summary>
        /// Internal array storing an array of all nodeId uint values. Lazy-initialized by NodeIdArray
        /// </summary>
        private uint[]? _nodeIdCache;
        #endregion


        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="Nodeset"/> class.
        /// </summary>
        public Nodeset()
        {
            Name = string.Empty;
            NodeAttributeDefinitionManager = new NodeAttributeDefinitionManager();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Nodeset"/> class, with the given name.
        /// The Nodeset is empty by default but this can be adjusted. If set to a positive integer,
        /// the created nodes will have Id numbers starting from 0.
        /// </summary>
        /// <param name="name">The name of the Nodeset.</param>
        /// <param name="createNodes">The number of nodes that are to be created from the start.</param>
        public Nodeset(string name, int createNodes = 0)
        {
            InitSizeNodesWithoutAttributes(createNodes);
            Name = name;
            NodeAttributeDefinitionManager = new NodeAttributeDefinitionManager();
            if (createNodes > 0)
                for (uint i = 0; i < createNodes; i++)
                    _addNodeWithoutAttribute(i);
            IsModified = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Nodeset"/> class, with the given name.
        /// Also creates a set of nodes based on the provided HashSet of unique unsigned integers.
        /// This constructor is mainly intended for import functions, when the nodeset is created
        /// based on the node Ids discovered during the edge parsing process.
        /// </summary>
        /// <param name="name">The name of the Nodeset</param>
        /// <param name="nodeIds">A HashSet of unique id numbers (uint) for the nodes to be created.</param>
        public Nodeset(string name, HashSet<uint> nodeIds)
        {
            Name = name;
            NodeAttributeDefinitionManager = new NodeAttributeDefinitionManager();
            _nodesWithoutAttributes = nodeIds;
        }
        #endregion


        #region Properties
        /// <summary>
        /// Gets or sets the name of the Nodeset
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns content info about this structure as a list of strings
        /// </summary>
        public List<string> Preview
        {
            get
            {
                List<string> lines = [$"Nodeset: {Name}"];
                int count = 10;
                foreach (uint nodeId in NodeIdArray)
                {
                    if (count <= 0)
                        break;
                    lines.Add($"Node {nodeId}");
                    if (_nodesWithAttributes.TryGetValue(nodeId, out var attributes))
                        for (int i = 0; i < attributes.AttrIndexes.Count; i++)
                            lines.Add($" {NodeAttributeDefinitionManager.IndexToName[attributes.AttrIndexes[i]]}: {attributes.AttrValues[i]}");
                    count--;
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
        /// Returns a dictionary with meta-information about this Nodeset, including its attributes.
        /// </summary>
        public Dictionary<string, object> Info => new Dictionary<string, object>
        {
            ["Type"] = "Nodeset",
            ["Name"] = Name,
            ["Filepath"] = Filepath,
            ["isModified"] = IsModified,
            ["NbrNodes"] = Count,
            ["NodeAttributes"] = NodeAttributeDefinitionManager.GetMetadataList()
        };

        /// <summary>
        /// Reference to the manager for node attribute definitions
        /// </summary>
        public NodeAttributeDefinitionManager NodeAttributeDefinitionManager { get; init; }

        /// <summary>
        /// Returns an array of all NodeId uint values. How this is done depends mainly
        /// on whether the 'nodecache' user setting is true or false. If active (true), the lazy-initialized
        /// nodeIdCache is used: first time this array is requested, the array is generated and stored in memory
        /// before being returned. Subsequent calls to NodeArray will return the same, already stored array.
        /// Note that this nodecache array will be deleted as soon as a new node is added or removed.
        /// If the 'nodecache' user setting is inactive (false), the array of Node objects is created on-the-fly.
        /// This saves memory - no need for permanent storing Nodes in array as well as in the dictionary - but
        /// the array must be created everytime it is called.
        /// </summary>
        /// <remarks>
        /// if picking a random node from the network many times while not adding/removing any nodes in between,
        /// or if memory isn't critical, the nodecache can be active (its default). If memory is critical and if 
        /// picking random nodes isn't crucial, it can be turned off.
        /// </remarks>
        public uint[] NodeIdArray
        {
            get
            {
                if (UserSettings.NodeCache)
                {
                    if (_nodeIdCache == null)
                        _nodeIdCache = _getAllNodeIds();
                    return _nodeIdCache;
                }
                return _getAllNodeIds();
            }
        }

        /// <summary>
        /// Returns an array of all NodeId uint values for nodes without node attributes.
        /// Used by binary writer as it separates between nodes with and without attributes.
        /// </summary>
        internal uint[] NodeIdArrayWithoutAttributes => _nodesWithoutAttributes.ToArray();

        /// <summary>
        /// Returns an array of all NodeId uint values for nodes with node attributes
        /// Used by binary reader as it separates between nodes with and without attributes.
        /// </summary>
        internal uint[] NodeIdArrayWithAttributes => _nodesWithAttributes.Keys.ToArray();

        /// <summary>
        /// Returns the number of nodes in this Nodeset.
        /// </summary>
        public int Count { get { return _nodesWithAttributes.Count + _nodesWithoutAttributes.Count; } }
        #endregion


        #region Methods (public)
        /// <summary>
        /// Adds a node with the specified nodeid to this Nodeset. Checks that it doesn't already exist.
        /// Initially the node has no attributes.
        /// </summary>
        /// <param name="nodeId">The id of the node that is to be added.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddNode(uint nodeId)
        {
            if (CheckThatNodeExists(nodeId))
                return OperationResult.Fail("NodeAlreadyExists", $"Node with ID '{nodeId}' already exists in nodeset '{Name}'.");
            _nodesWithoutAttributes.Add(nodeId);
            _modified();
            return OperationResult.Ok($"Node ID '{nodeId}' added to nodeset '{Name}'.");
        }

        /// <summary>
        /// Removes the specified node id from this Nodeset, setting the Nodeset as being modified.
        /// </summary>
        /// <param name="nodeId">The id of the node that is to be removed.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveNode(uint nodeId)
        {
            if (_nodesWithoutAttributes.Remove(nodeId) || _nodesWithAttributes.Remove(nodeId))
            {
                _modified();
                return OperationResult.Ok($"Node '{nodeId}' removed from nodeset '{Name}'.");
            }
            return OperationResult.Fail("NodeNotFound", $"Node '{nodeId}' not found in nodeset '{Name}'.");
        }

        /// <summary>
        /// Returns the node id specified by its index position in the NodeIdArray
        /// Useful for traversing all nodes (when its better to have the nodecache active)
        /// </summary>
        /// <param name="index">The index position of the node.</param>
        /// <returns>The Node id or null if no node found at this index.</returns>
        public uint? GetNodeIdByIndex(uint index)
        {
            if (index < NodeIdArray.Length)
                return NodeIdArray[index];
            return null;
        }

        /// <summary>
        /// Defines a new node attribute for this Nodeset with the given name and the specific attribute type
        /// as given by the provided string representation. Acts as a wrapper to the subsequent
        /// <see cref="DefineNodeAttribute(string, NodeAttributeType)"/> method.
        /// </summary>
        /// <param name="attrName">The name of the nodal attribute (must be unique in this Nodeset).</param>
        /// <param name="attributeType">The textual representation of this node attribute type (either 'char','int','float', or 'bool').</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult DefineNodeAttribute(string attrName, string attrTypeStr)
        {
            if (!(Misc.GetAttributeType(attrTypeStr) is NodeAttributeType attrType))
                return OperationResult.Fail("InvalidAttributeType", $"Attribute type '{attrTypeStr}' not recognized.");
            return DefineNodeAttribute(attrName, attrType);
        }

        /// <summary>
        /// Undefines the node attribute with the specified name. This will also remove this attribute from all nodes.
        /// </summary>
        /// <param name="attrName">The name of the nodal attribute to undefine.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult UndefineNodeAttribute(string attrName)
        {
            OperationResult<byte> result = NodeAttributeDefinitionManager.UndefineNodeAttribute(attrName);
            if (!result.Success)
                return result;

            // Remove this attribute from all nodes and possibly move nodes to the collection for nodes without attributes
            byte oldAttrIndex = result.Value;
            List<uint> nodesNowWithoutAttributes = new();
            foreach (var nodeId in _nodesWithAttributes.Keys)
            {
                if (!RemoveNodeAttribute(nodeId, oldAttrIndex))
                    // The node has no more attributes left - mark it for moving to Hashset
                    nodesNowWithoutAttributes.Add(nodeId);
            }
            // Iterate through nodes that now lost all attributes: move them to the HashSet instead
            foreach (uint nodeId in nodesNowWithoutAttributes)
            {
                _nodesWithAttributes.Remove(nodeId);
                _nodesWithoutAttributes.Add(nodeId);
            }
            return OperationResult.Ok($"Node attribute '{attrName}' is no longer defined.");
        }

        /// <summary>
        /// Sets the specific attrValue for the node attribute of the selected node id.
        /// </summary>
        /// <param name="nodeId">The unique id of the node</param>
        /// <param name="attrName">The name of the node attribute.</param>
        /// <param name="attrValueStr">The attrValue of this attribute expressed as a string.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult SetNodeAttribute(uint nodeId, string attrName, string attrValueStr)
        {
            if (!CheckThatNodeExists(nodeId))
                return OperationResult.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult.Fail("AttributeNotFound", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                return OperationResult.Fail("AttributeTypeNotFound", $"No type found for attribute '{attrName}' in nodeset '{Name}': possibly corrupted.");
            if (!(Misc.CreateNodeAttributeValueFromAttributeTypeAndValueString(attrType, attrValueStr) is NodeAttributeValue attrValue))
                return OperationResult.Fail("ParseAttributeValueError", $"Could not convert string '{attrValueStr}' to type '{attrType}'.");
            SetNodeAttribute(nodeId, attrIndex, attrValue);
            return OperationResult.Ok($"Attribute '{attrName}' for node {nodeId} set to {attrValue}.");
        }

        /// <summary>
        /// Gets the specific attrValue for the node attribute of the selected node id.
        /// </summary>
        /// <param name="nodeId">The unique id of the node.</param>
        /// <param name="attrName">The name of the node attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="NodeAttributeValue"/>.</returns>
        public OperationResult<NodeAttributeValue> GetNodeAttribute(uint nodeId, string attrName)
        {
            if (!CheckThatNodeExists(nodeId))
                return OperationResult<NodeAttributeValue>.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult<NodeAttributeValue>.Fail("AttributeNotFound", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");
            if (!(GetNodeAttribute(nodeId, attrIndex) is NodeAttributeValue attrValue))
                return OperationResult<NodeAttributeValue>.Fail("AttributeNotFound", $"Attribute '{attrName}' not set for node '{nodeId}' in nodeset '{Name}'.");
            return OperationResult<NodeAttributeValue>.Ok(attrValue);
        }

        public OperationResult<Dictionary<uint, object?>> GetMultipleNodeAttributes(uint[] nodeIds, string attrName)
        {
            if (!NodeAttributeDefinitionManager.CheckIfAttributeNameExists(attrName))
                return OperationResult<Dictionary<uint, object?>>.Fail("AttributeNotFound", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");

            var result = new Dictionary<uint, object?>();
            foreach (uint nodeId in nodeIds)
            {
                if (!CheckThatNodeExists(nodeId))
                    continue;
                var attrResult = GetNodeAttribute(nodeId, attrName);
                if (attrResult.Success)
                    result[nodeId] = attrResult.Value.GetValue();
                else
                    result[nodeId] = null;
            }
            return OperationResult<Dictionary<uint, object?>>.Ok(result);
        }

        /// <summary>
        /// Remove specific node attribute from the selected node id.
        /// If the node has no more attributes left, move it to the container for nodes without attributes
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrName">The name of the node attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveNodeAttribute(uint nodeId, string attrName)
        {
            if (!CheckThatNodeExists(nodeId))
                return OperationResult.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult.Fail("AttributeNotFound", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");
            if (!_nodesWithAttributes.TryGetValue(nodeId, out var attributes))
                return OperationResult<NodeAttributeValue>.Fail("AttributeNotFound", $"Attribute '{attrName}' not set for node '{nodeId}' in nodeset '{Name}'.");
            if (!_removeAttributeFromTuple(attributes, attrIndex))
            {
                _nodesWithAttributes.Remove(nodeId);
                _nodesWithoutAttributes.Add(nodeId);
            }
            return OperationResult.Ok($"Attribute '{attrName}' removed for node {nodeId}.");
        }

        /// <summary>
        /// Returns a list of node ids in this nodeset.
        /// Returns a maximum of 1000 nodes starting with the first one in the set of nodes, but
        /// this can be adjusted.
        /// </summary>
        /// <param name="offset">The index of the node to start with (defaults to 0).</param>
        /// <param name="limit">The maximum number of nodes to return.</param>
        /// <returns>Returns an OperationResult object with the list of node ids (nodeIds) (given the offset and limit).</returns>
        public OperationResult<List<uint>> GetAllNodes(int offset = 0, int limit = 1000)
        {
            offset = (offset < 0) ? 0 : offset;
            limit = (limit < 0) ? 0 : limit;
            uint[] allNodes = NodeIdArray;
            int total = NodeIdArray.Length;
            var nodes = allNodes.Skip(offset).Take(limit).ToList();
            string message;
            if (total == 0)
                message = $"Nodeset '{Name}' has no nodes.";
            else if (nodes.Count == 0)
                message = $"Offset {offset} is beyond the available nodes in nodeset '{Name}' (total: {total}).";
            else if (offset == 0 && nodes.Count == total)
                message = $"Returning all {total} nodes in nodeset '{Name}':";
            else
                message = $"Returning nodes {offset + 1} - {offset + nodes.Count} of {total} in nodeset '{Name}':";
            return OperationResult<List<uint>>.Ok(nodes, message);
        }
        #endregion


        #region Method (internal)
        /// <summary>
        /// Checks if the Nodeset contains the two nodes with the specified ids.
        /// This one returns a proper OperationResult informing which, or if both, were missing.
        /// </summary>
        /// <param name="node1Id">The id of the first node that is to be checked.</param>
        /// <param name="node2Id">The id of the second node that is to be checked.</param>
        /// <returns>An <see cref="OperationResult.Success"> if both are found, Fail otherwise.</returns>
        internal OperationResult CheckThatNodesExist(uint node1Id, uint node2Id)
        {
            bool node1exists = CheckThatNodeExists(node1Id);
            bool node2exists = CheckThatNodeExists(node2Id);
            if (node1exists && node2exists)
                return OperationResult.Ok();
            if (!node1exists && !node2exists)
                return OperationResult.Fail("NodeNotFound", $"Neither node {node1Id} nor node {node2Id} found in nodeset '{Name}'.");
            if (!node1exists)
                return OperationResult.Fail("NodeNotFound", $"Node {node1Id} not found in nodeset '{Name}'.");
            return OperationResult.Fail("NodeNotFound", $"Node {node2Id} not found in nodeset '{Name}'.");
        }

        /// <summary>
        /// Defines a new node attribute for this Nodeset with the given name and
        /// <see cref="NodeAttributeType"/>. Each node attribute in a <see cref="Nodeset"> must have a unique name.
        /// Whereas <see cref="DefineNodeAttribute(string, string)"/> provides the node attribute as a string, this
        /// method provides it as a <see cref="NodeAttributeType"/> enum.
        /// Returns the attribute index of the created node attribute if successful.
        /// </summary>
        /// <param name="attrName">The name of the nodal attribute (must be unique in this Nodeset).</param>
        /// <param name="attrType">The type (using the <see cref="NodeAttributeType"/> enum) of this attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, including the internal attribute index of the
        /// added attribute.</returns>
        internal OperationResult<byte> DefineNodeAttribute(string attrName, NodeAttributeType attrType)
        {
            OperationResult<byte> result = NodeAttributeDefinitionManager.DefineNewNodeAttribute(attrName, attrType);
            if (result.Success)
                _modified();
            return result;
        }

        /// <summary>
        /// Defines and sets node attribute values for multiple nodes based on the provided dictionary of nodeId=>attributeValueStr.
        /// If the attribute is already defined, it checks that the provided type matches the existing type. If it exists but with a different type,
        /// an error is returned. If the attribute is not yet defined, it is defined based on the provided type.
        /// </summary>
        /// <param name="attrName">The name of the node attribute.</param>
        /// <param name="attrDict">The dictionary of nodeId=>attributeValueStr.</param>
        /// <param name="attrType">The type of the node attribute.</param>
        /// <returns>Returns an <see cref="OperationResult"/> informing how well it went.</returns>
        internal OperationResult DefineAndSetNodeAttributeValues(string attrName, Dictionary<uint, string> attrDict, NodeAttributeType attrType)
        {
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
            {
                OperationResult<byte> defineresult = DefineNodeAttribute(attrName, attrType);
                if (!defineresult.Success)
                    return defineresult;
                attrIndex = defineresult.Value;
            }
            else
            {
                if (NodeAttributeDefinitionManager.IndexToType[attrIndex] != attrType)
                    return OperationResult.Fail("ConstraintAttributeTypeMismatch", $"Attribute '{attrName}' already defined with type '{NodeAttributeDefinitionManager.IndexToType[attrIndex]}', cannot set values of type '{attrType}'.");
            }
            foreach ((uint nodeId, string attrValueStr) in attrDict)
                SetNodeAttribute(nodeId, attrName, attrValueStr);
            _modified();
            return OperationResult.Ok($"Node attribute '{attrName}' set for {attrDict.Count} nodes in nodeset '{Name}'.");
        }

        /// <summary>
        /// Adds a Node to the collection of nodes without attributes.
        /// No verification is done: to be used with loaders.
        /// </summary>
        /// <param name="nodeId">The node id to add.</param>
        internal void _addNodeWithoutAttribute(uint nodeId)
        {
            _nodesWithoutAttributes.Add(nodeId);
        }

        /// <summary>
        /// Adds a node with the specified nodeId to this Nodeset. Possible to provide an optional attribute tuple
        /// where node attribute indices are mapped on <see cref="NodeAttributeValue"/> objects.
        /// </summary>
        /// <param name="nodeId">The node id to add.</param>
        /// <param name="nodeAttributes">A tuple with index and attrValue lists for attributes.</param>
        internal void _addNodeWithAttributes(uint nodeId, (List<byte> attrIndexes, List<NodeAttributeValue> attrValues)? nodeAttributes)
        {
            if (nodeAttributes != null && nodeAttributes.Value.attrIndexes.Count > 0)
                _nodesWithAttributes[nodeId] = (nodeAttributes.Value.attrIndexes, nodeAttributes.Value.attrValues);
            else
                _nodesWithoutAttributes.Add(nodeId);
        }

        /// <summary>
        /// Adds a Node to the collection of nodes with attributes, though with no attributes yet.
        /// To be used with loader where it is known that attributes will be added.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        internal void _addNodeWithAttribute(uint nodeId)
        {
            _nodesWithAttributes.Add(nodeId, ([], []));
        }

        /// <summary>
        /// Checks if the Nodeset contains a node object with the specified id.
        /// </summary>
        /// <param name="nodeId">The id of the node that is to be checked.</param>
        /// <returns>True if the node id exists, false otherwise.</returns>
        internal bool CheckThatNodeExists(uint nodeId)
        {
            return _nodesWithoutAttributes.Contains(nodeId) || _nodesWithAttributes.ContainsKey(nodeId);
        }

        /// <summary>
        /// Adds a node attribute directly, without any validation whatsoever.
        /// Note that this does not check if the attribute index already exists.
        /// Only to use with loaders where it is assumed that data is already validated.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrIndex">The attribute index (as specified in NodeAttributeDefinitionManager).</param>
        /// <param name="attrValue">The NodeAttributeValue attrValue</param>
        internal void AddNodeAttribute(uint nodeId, byte attrIndex, NodeAttributeValue attrValue)
        {
            _nodesWithAttributes[nodeId].AttrIndexes.Add(attrIndex);
            _nodesWithAttributes[nodeId].AttrValues.Add(attrValue);
        }

        /// <summary>
        /// Sets a node attribute for a specific node, given the attribute index and attribute attrValue.
        /// If node lacks attributes, it is moved to the dictionary for attributes and the new attribute is installed.
        /// If the node has attributes, it first checks if this attribute exists: if so, it overwrites the existing attrValue.
        /// Otherwise: it adds this as a new attribute.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <param name="attrValue">The <see cref="NodeAttributeValue"/> to set the attribute to.</param>
        internal void SetNodeAttribute(uint nodeId, byte attrIndex, NodeAttributeValue attrValue)
        {
            if (!_nodesWithAttributes.TryGetValue(nodeId, out var attributes))
            {
                _nodesWithoutAttributes.Remove(nodeId);
                _nodesWithAttributes.Add(nodeId, (new() { attrIndex }, new() { attrValue }));
            }
            else
            {
                int index = attributes.AttrIndexes.IndexOf(attrIndex);
                if (index >= 0)
                    attributes.AttrValues[index] = attrValue;
                else
                {
                    attributes.AttrIndexes.Add(attrIndex);
                    attributes.AttrValues.Add(attrValue);
                }
            }
        }

        /// <summary>
        /// Gets the specified attribute from a specific node id based on the attribute index.
        /// Returns null if the node does not have this attribute (or no attributes whatsoever)
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <returns>Returns a <see cref="NodeAttributeValue"/>, or null if attribute is missing.</returns>
        internal NodeAttributeValue? GetNodeAttribute(uint nodeId, byte attrIndex)
        {
            if (!_nodesWithAttributes.TryGetValue(nodeId, out var attributes))
                return null;
            int index = attributes.AttrIndexes.IndexOf(attrIndex);
            if (index >= 0)
                return attributes.AttrValues[index];
            return null;
        }

        /// <summary>
        /// Removes the specified attribute from a node and shifts its lists of attribute indexes and attribute values.
        /// Returns false if it doesn't have any attributes before or after removal. Returns true if it has attributes
        /// left after removal.
        /// </summary>
        /// <param name="nodeId">The node id</param>
        /// <param name="attrIndex">The attribute index</param>
        /// <returns>Returns true if it still has attributes left, false if it has no attributes before or after removal.</returns>
        internal bool RemoveNodeAttribute(uint nodeId, byte attrIndex)
        {
            if (!_nodesWithAttributes.TryGetValue(nodeId, out var attributes))
                return false;
            return _removeAttributeFromTuple(attributes, attrIndex);
        }

        /// <summary>
        /// Clones the attribute tuple for a specific node. Returns null if the node lacks attributes.
        /// </summary>
        /// <param name="nodeId">The node id</param>
        /// <returns>The attribute tuple, or null if missing.</returns>
        internal (List<byte>, List<NodeAttributeValue>)? CloneNodeAttributeTuple(uint nodeId)
        {
            if (_nodesWithAttributes.TryGetValue(nodeId, out var attributes))
            {
                return (new List<byte>(attributes.AttrIndexes), new List<NodeAttributeValue>(attributes.AttrValues));
            }
            return null;
        }

        /// <summary>
        /// Gets the node attribute tuple for a specific node id. Returns null if it lacks attributes.
        /// </summary>
        /// <param name="nodeId">The node id</param>
        /// <returns>The attribute tuple, or null if missing.</returns>
        internal (List<byte> AttrIndexes, List<NodeAttributeValue> AttrValues)? GetNodeAttributeTuple(uint nodeId)
        {
            if (_nodesWithAttributes.TryGetValue(nodeId, out var attributes))
                return attributes;
            return null;
        }

        /// <summary>
        /// Given an array of node ids, creates a new array of node ids that only includes each
        /// node id once and where the node id is part of this Nodeset object.
        /// </summary>
        /// <param name="nodeIds">The array of node ids to check.</param>
        /// <returns>An array of unique node ids that are also part of this Nodeset.</returns>
        internal uint[] FilterOutNonExistingNodeIds(uint[] nodeIds)
        {
            HashSet<uint> existing = new();
            foreach (uint nodeId in nodeIds)
                if (CheckThatNodeExists(nodeId))
                    existing.Add(nodeId);
            return existing.ToArray();
        }

        /// <summary>
        /// Initialize the capacity of the Hashset storing nodes without attributes
        /// </summary>
        /// <param name="nbrNodesWithoutAttributes">Upper range of number of nodes</param>
        internal void InitSizeNodesWithoutAttributes(int nbrNodesWithoutAttributes)
        {
            _nodesWithoutAttributes = new(nbrNodesWithoutAttributes);
        }

        /// <summary>
        /// Initialize the capacity of the Dictionary storing nodes with attributes
        /// </summary>
        /// <param name="nbrNodesWithAttributes">Upper range of number of nodes</param>
        internal void InitSizeNodesWithAttributes(int nbrNodesWithAttributes)
        {
            _nodesWithAttributes = new(nbrNodesWithAttributes);
        }
        #endregion


        #region Private methods
        /// <summary>
        /// Support function for new nodeCache to get all nodeIds
        /// </summary>
        /// <returns></returns>
        private uint[] _getAllNodeIds()
        {
            int totalNbrNodes = _nodesWithAttributes.Count + _nodesWithoutAttributes.Count;
            uint[] allNodes = new uint[totalNbrNodes];
            _nodesWithAttributes.Keys.CopyTo(allNodes, 0);
            _nodesWithoutAttributes.CopyTo(allNodes, _nodesWithAttributes.Count);
            Array.Sort(allNodes);
            return allNodes;
        }

        /// <summary>
        /// Methods that flags the Nodeset as being modified since last load/save, also
        /// clearing the _nodeIdCache
        /// </summary>
        private void _modified()
        {
            IsModified = true;
            _nodeIdCache = null;

        }

        /// <summary>
        /// Removes the specified attribute from a attribute tuple and 'shifts' the items
        /// (places the last item where this was).
        /// </summary>
        /// <param name="nodeId">The node id</param>
        /// <param name="attributeIndex">The attribute index</param>
        /// <returns>Returns true if it still has attributes left, false if all attributes are gone now.</returns>
        private bool _removeAttributeFromTuple((List<byte> AttrIndexes, List<NodeAttributeValue> AttrValues) attributes, byte attributeIndex)
        {
            int index = attributes.AttrIndexes.IndexOf(attributeIndex);
            if (index >= 0)
            {
                int lastIndex = attributes.AttrIndexes.Count - 1;
                attributes.AttrIndexes[index] = attributes.AttrIndexes[lastIndex];
                attributes.AttrValues[index] = attributes.AttrValues[lastIndex];

                attributes.AttrIndexes.RemoveAt(lastIndex);
                attributes.AttrValues.RemoveAt(lastIndex);
            }
            return (attributes.AttrIndexes.Count > 0);
        }
        #endregion
    }
}
