using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        private Dictionary<uint, Dictionary<uint, NodeAttributeValue>> _nodesWithAttributes = new();

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
            _initSizeWithoutAttributes(createNodes);
            Name = name;
            NodeAttributeDefinitionManager = new NodeAttributeDefinitionManager();
            if (createNodes > 0)
                for (uint i = 0; i < createNodes; i++)
                    _AddNodeWithoutAttribute(i);
                    //AddNode(i);
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
        public List<string> Content
        {
            get
            {
                List<string> lines = [$"Nodeset: {Name}"];
                foreach (uint nodeId in NodeIdArray)
                {
                    lines.Add($"Node {nodeId}");
                    if (_nodesWithAttributes.TryGetValue(nodeId, out var attrDict))
                    {
                        foreach (var kvp in attrDict)
                            lines.Add($" {NodeAttributeDefinitionManager.IndexToName[kvp.Key]}: {kvp.Value}");                            
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
        internal uint[] NodeIdArray
        {
            get
            {
                if (UserSettings.NodeCache)
                {
                    if (_nodeIdCache == null)
                        _nodeIdCache = GetAllNodeIds();
                    return _nodeIdCache;
                }
                return GetAllNodeIds();
            }
        }

        /// <summary>
        /// Returns an array of all NodeId uint values for nodes without node attributes.
        /// Used by binary writer.
        /// </summary>
        internal uint[] NodeIdArrayWithoutAttributes => _nodesWithoutAttributes.ToArray();

        /// <summary>
        /// Returns an array of all NodeId uint values for nodes with node attributes
        /// Used by binary reader.
        /// </summary>
        internal uint[] NodeIdArrayWithAttributes => _nodesWithAttributes.Keys.ToArray();


        /// <summary>
        /// Returns the number of nodes in this Nodeset.
        /// </summary>
        public int Count { get { return _nodesWithAttributes.Count + _nodesWithoutAttributes.Count; } }
        #endregion


        #region Methods (public)
        /// <summary>
        /// Adds a node with the specified nodeid to this Nodeset.
        /// </summary>
        /// <param name="nodeId">The id of the node that is to be added.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddNode(uint nodeId)
        {
            if (CheckThatNodeExists(nodeId))
                return OperationResult.Fail("NodeAlreadyExists", $"Node with ID '{nodeId}' already exists in nodeset '{Name}'.");
            _nodesWithoutAttributes.Add(nodeId);
            Modified();
            return OperationResult.Ok($"Node ID {nodeId} added to nodeset '{Name}'.");
        }

        /// <summary>
        /// Adds a node with the specified nodeId to this Nodeset. Possible to provide an optional attribute dictionary
        /// where node attribute indices are mapped on <see cref="NodeAttributeValue"/> objects.
        /// </summary>
        /// <param name="nodeId">The id of the node.</param>
        /// <param name="nodeAttrDict">An optional dictionary of node attributes for this node.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult AddNode(uint nodeId, Dictionary<uint, NodeAttributeValue>? nodeAttrDict)
        {
            if (CheckThatNodeExists(nodeId))
                return OperationResult.Fail("NodeAlreadyExists", $"Node with ID '{nodeId}' already exists in nodeset '{Name}'.");
            if (nodeAttrDict != null && nodeAttrDict.Count > 0)
                _nodesWithAttributes[nodeId] = nodeAttrDict;
            else
                _nodesWithoutAttributes.Add(nodeId);
            Modified();
            return OperationResult.Ok($"Node ID {nodeId} added to nodeset '{Name}'.");
        }

        /// <summary>
        /// Removes the specified node id from this Nodeset. Also clears the nodecache.
        /// </summary>
        /// <param name="nodeId">The id of the node that is to be removed.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveNode(uint nodeId)
        {
            if (_nodesWithoutAttributes.Remove(nodeId) || _nodesWithAttributes.Remove(nodeId))
            {
                Modified();
                return OperationResult.Ok($"Node '{nodeId}' removed from nodeset '{Name}'.");
            }
            return OperationResult.Fail("NodeNotFound", $"Node '{nodeId}' not found in nodeset '{Name}'.");
        }

        /// <summary>
        /// Returns the node id specified by its index position in the NodeIdArray
        /// Useful for traversing all nodes
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
        /// <param name="attributeName">The name of the nodal attribute (must be unique in this Nodeset).</param>
        /// <param name="attributeType">The textual representation of this node attribute type (either 'char','int','float', or 'bool').</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult DefineNodeAttribute(string attributeName, string attributeTypeStr)
        {
            if (!(Misc.GetAttributeType(attributeTypeStr) is NodeAttributeType attributeType))
                return OperationResult.Fail("AttributeTypeUnknown", $"Attribute type '{attributeTypeStr}' not recognized.");
            return DefineNodeAttribute(attributeName, attributeType);
        }

        /// <summary>
        /// Undefines the node attribute with the specified name. This will also remove this attribute from all nodes.
        /// </summary>
        /// <param name="attributeName">The name of the nodal attribute to undefine.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult UndefineNodeAttribute(string attributeName)
        {
            OperationResult<uint> result = NodeAttributeDefinitionManager.UndefineNodeAttribute(attributeName);
            if (!result.Success)
                return result;
            uint oldAttributeIndex = result.Value;
            List<uint> nodesToRemove = new();
            foreach (var kvp in _nodesWithAttributes)
                if (kvp.Value.ContainsKey(oldAttributeIndex) && kvp.Value.Remove(oldAttributeIndex) && kvp.Value.Count == 0)
                    nodesToRemove.Add(kvp.Key);
            if (nodesToRemove.Count > 0)
            {
                Modified();
                foreach (uint nodeId in nodesToRemove)
                {
                    _nodesWithAttributes.Remove(nodeId);
                    _nodesWithoutAttributes.Add(nodeId);
                }
            }
            return OperationResult.Ok($"Node attribute '{attributeName}' is no longer defined.");
        }

        /// <summary>
        /// Sets the specific value for the node attribute of the selected node id.
        /// </summary>
        /// <param name="nodeId">The unique id of the node</param>
        /// <param name="attributeName">The name of the node attribute.</param>
        /// <param name="valueStr">The value of this attribute expressed as a string.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult SetNodeAttribute(uint nodeId, string attributeName, string valueStr)
        {
            if (!CheckThatNodeExists(nodeId))
                return OperationResult.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attributeName, out uint attrIndex))
                return OperationResult.Fail("AttributeNameNotFound", $"Unknown attribute '{attributeName}' in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                return OperationResult.Fail("AttributeTypeNotFound", $"No type found for attribute '{attributeName}' in nodeset '{Name}': possibly corrupted.");
            if (!(Misc.CreateNodeAttributeValueFromAttributeTypeAndValueString(attrType, valueStr) is NodeAttributeValue nodeAttribute))
                return OperationResult.Fail("StringConversionError", $"Could not convert string '{valueStr}' to type '{attrType}'.");

            SetNodeAttribute(nodeId, attrIndex, nodeAttribute);

            //if (!_nodesWithAttributes.TryGetValue(nodeId, out var attrDict))
            //{
            //    _nodesWithoutAttributes.Remove(nodeId);
            //    attrDict = new Dictionary<uint, NodeAttributeValue>();
            //    _nodesWithAttributes.Add(nodeId, attrDict);
            //}
            //attrDict[attrIndex] = nodeAttribute;
            Modified();
            return OperationResult.Ok($"Attribute '{attributeName}' for node {nodeId} set to {nodeAttribute}.");
        }


        /// <summary>
        /// Gets the specific value for the node attribute of the selected node id.
        /// </summary>
        /// <param name="nodeId">The unique id of the node.</param>
        /// <param name="attributeName">The name of the node attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="NodeAttributeValue"/>.</returns>
        public OperationResult<NodeAttributeValue> GetNodeAttribute(uint nodeId, string attributeName)
        {
            if (!CheckThatNodeExists(nodeId))
                return OperationResult<NodeAttributeValue>.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attributeName, out uint attrIndex))
                return OperationResult<NodeAttributeValue>.Fail("AttributeNameNotFound", $"Unknown attribute '{attributeName}' in nodeset '{Name}'.");
            if (!_nodesWithAttributes.TryGetValue(nodeId, out var attrDict) || !attrDict.TryGetValue(attrIndex, out NodeAttributeValue nodeAttribute))
                return OperationResult<NodeAttributeValue>.Fail("AttributeNotFound", $"Attribute '{attributeName}' not set for node '{nodeId}' in nodeset '{Name}'.");
            return OperationResult<NodeAttributeValue>.Ok(nodeAttribute);
        }

        /// <summary>
        /// Remove the specific node attribute of the selected node id.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="attributeName"></param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RemoveNodeAttribute(uint nodeId, string attributeName)
        {
            if (!CheckThatNodeExists(nodeId))
                return OperationResult.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attributeName, out uint attrIndex))
                return OperationResult.Fail("AttributeNameNotFound", $"Unknown attribute '{attributeName}' in nodeset '{Name}'.");
            if (!_nodesWithAttributes.TryGetValue(nodeId, out var attrDict) || !attrDict.ContainsKey(attrIndex))
                return OperationResult<NodeAttributeValue>.Fail("AttributeNotFound", $"Attribute '{attributeName}' not set for node '{nodeId}' in nodeset '{Name}'.");
            attrDict.Remove(attrIndex);
            Modified();
            if (attrDict.Count == 0)
            {
                _nodesWithAttributes.Remove(nodeId);
                _nodesWithoutAttributes.Add(nodeId);
            }
            return OperationResult.Ok($"Attribute '{attributeName}' removed for node {nodeId}.");
        }
        #endregion


        #region Method (internal)
        /// <summary>
        /// Adds a Node to the collection of nodes without attributes.
        /// No verification is done: to be used with loaders.
        /// </summary>
        /// <param name="nodeId">The node id to add</param>
        internal void _AddNodeWithoutAttribute(uint nodeId)
        {
            _nodesWithoutAttributes.Add(nodeId);
        }

        /// <summary>
        /// Adds a Node to the collection of nodes with attributes.
        /// No verification is done: to be used with loaders.
        /// </summary>
        /// <param name="nodeId"></param>
        internal void _AddNodeWithAttribute(uint nodeId)
        {
            _nodesWithAttributes.Add(nodeId, new Dictionary<uint, NodeAttributeValue>());
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
        /// Checks if the Nodeset contains the two nodes with the specified ids.
        /// </summary>
        /// <param name="node1id">The id of the first node that is to be checked.</param>
        /// <param name="node2id">The id of the second node that is to be checked.</param>
        /// <returns>An <see cref="OperationResult.Success"> if both are found, Fail otherwise.</returns>
        internal OperationResult CheckThatNodesExist(uint node1id, uint node2id)
        {
            bool node1exists = CheckThatNodeExists(node1id);
            bool node2exists = CheckThatNodeExists(node2id);
            if (node1exists && node2exists)
                return OperationResult.Ok();
            if (!node1exists && !node2exists)
                return OperationResult.Fail("NodeNotFound", $"Neither node {node1id} nor node {node2id} found in nodeset '{Name}'.");
            if (!node1exists)
                return OperationResult.Fail("NodeNotFound", $"Node {node1id} not found in nodeset '{Name}'.");
            return OperationResult.Fail("NodeNotFound", $"Node {node2id} not found in nodeset '{Name}'.");
        }

        /// <summary>
        /// Clones the current Nodeset, creating a new NodeAttributeDefinitionManager and copying its
        /// content. Copies the node ids
        /// </summary>
        /// <param name="newName"></param>
        /// <param name="cloneNodes"></param>
        /// <returns></returns>
        internal Nodeset Clone(string? newName = null, bool cloneNodes = true)
        {
            Nodeset clone = new Nodeset(newName != null ? newName : Name + "_clone") { NodeAttributeDefinitionManager = NodeAttributeDefinitionManager.Clone() };
            if (cloneNodes)
            {
                foreach (var (nodeId, attrDict) in _nodesWithAttributes)
                    clone._nodesWithAttributes[nodeId] = new Dictionary<uint, NodeAttributeValue>(attrDict);
                foreach (uint nodeId in _nodesWithoutAttributes)
                    clone._nodesWithoutAttributes.Add(nodeId);
            }
            return clone;
        }

        /// <summary>
        /// Defines a new node attribute for this Nodeset with the given name and
        /// <see cref="NodeAttributeType"/>. Each node attribute in a <see cref="Nodeset"> must have a unique name.
        /// </summary>
        /// <param name="attributeName">The name of the nodal attribute (must be unique in this Nodeset).</param>
        /// <param name="attributeType">The type (using the <see cref="NodeAttributeType"/> enum) of this attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        internal OperationResult<uint> DefineNodeAttribute(string attributeName, NodeAttributeType attributeType)
        {
            OperationResult<uint> result = NodeAttributeDefinitionManager.DefineNewNodeAttribute(attributeName, attributeType);
            if (result.Success)
                Modified();
            return result;
        }

        /// <summary>
        /// Sets a node attribute directly, without any validation anywhere.
        /// To use with loaders.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrIndex">The attribute index in NodeAttributeDefinitionManager</param>
        /// <param name="value">The NodeAttributeValue value</param>
        internal void _setNodeAttribute(uint nodeId, uint attrIndex, NodeAttributeValue value)
        {
            _nodesWithAttributes[nodeId].Add(attrIndex, value);
        }

        internal OperationResult SetNodeAttribute(uint nodeId, string attributeName, NodeAttributeValue value)
        {
            try
            {
                if (!CheckThatNodeExists(nodeId))
                    return OperationResult.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
                if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attributeName, out uint attrIndex))
                    return OperationResult.Fail("AttributeNameNotFound", $"Unknown attribute '{attributeName}' in nodeset '{Name}'.");
                if (!NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                    return OperationResult.Fail("AttributeTypeNotFound", $"No type found for attribute '{attributeName}' in nodeset '{Name}': possibly corrupted.");
                if (attrType != value.Type)
                    return OperationResult.Fail("AttributeTypeMismatch", $"The provided data type ({value.Type}) does not match the value type for attribute '{attributeName}': ({attrType})");
                SetNodeAttribute(nodeId, attrIndex, value);
                return OperationResult.Ok();
                
            }
            catch (Exception e)
            {
                return OperationResult.Fail("NodeAttributeError", $"Error setting node attribute '{attributeName}' for node '{nodeId}' to '{value.ToString()}': {e.Message}");
            }
        }

        private void SetNodeAttribute(uint nodeId, uint attrIndex, NodeAttributeValue nodeAttribute)
        {
            if (!_nodesWithAttributes.TryGetValue(nodeId, out var attrDict))
            {
                _nodesWithoutAttributes.Remove(nodeId);
                attrDict = new Dictionary<uint, NodeAttributeValue>();
                _nodesWithAttributes.Add(nodeId, attrDict);
            }
            attrDict[attrIndex] = nodeAttribute;
        }


        /// <summary>
        /// Clones the internal attribute dictionary for a particular node. If it doesn't have attributes, return null
        /// </summary>
        /// <param name="nodeId">Id of the node.</param>
        /// <returns>Returns a dictionary with byte=>NodeAttributeValues.</byte></returns>
        internal Dictionary<uint, NodeAttributeValue>? CloneNodeAttributeDictionary(uint nodeId)
        {
            if (_nodesWithAttributes.TryGetValue(nodeId, out var attrDict))
                return new Dictionary<uint, NodeAttributeValue>(attrDict);
            return null;
        }

        internal Dictionary<string, NodeAttributeValue> GetNodeAttributeValues(uint nodeId)
        {
            Dictionary<string, NodeAttributeValue> nodeAttributeValues = [];
            if (_nodesWithAttributes.TryGetValue(nodeId, out var nodeAttrDict))
            {
                foreach (var nodeAttr in nodeAttrDict)
                {
                    if (NodeAttributeDefinitionManager.TryGetAttributeName(nodeAttr.Key, out var attrName))
                        nodeAttributeValues.Add(attrName, nodeAttr.Value);
                }
            }
            return nodeAttributeValues;
        }

        //internal string[] GetExistingAttributeNamesForNode(uint nodeId)
        //{
        //    List<string> existingNodeAttributeNames = [];
        //    if (_nodesWithAttributes.TryGetValue(nodeId, out var attrDict))
        //    {
        //        uint[] keys = attrDict.Keys.ToArray();
        //    }
                

        //    return existingNodeAttributeNames.ToArray();
        //}




        /// <summary>
        /// Defines and sets node attribute values for multiple nodes based on the provided dictionary of nodeId=>attributeValueStr.
        /// If the attribute is already defined, it checks that the provided type matches the existing type. If it exists but with a different type,
        /// an error is returned. If the attribute is not yet defined, it is defined based on the provided type.
        /// </summary>
        /// <param name="attrName">The name of the node attribute.</param>
        /// <param name="attrDict">The dictionary of nodeId=>attributeValueStr.</param>
        /// <param name="nodeAttributeType">The type of the node attribute.</param>
        /// <returns>Returns an <see cref="OperationResult"/> informing how well it went.</returns>
        internal OperationResult DefineAndSetNodeAttributeValues(string attrName, Dictionary<uint, string> attrDict, NodeAttributeType nodeAttributeType)
        {
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out uint attrIndex))
            {
                OperationResult<uint> defineresult = DefineNodeAttribute(attrName, nodeAttributeType);
                if (!defineresult.Success)
                    return defineresult;
                attrIndex = defineresult.Value;
            }
            else
            {
                if (NodeAttributeDefinitionManager.IndexToType[attrIndex] != nodeAttributeType)
                    return OperationResult.Fail("AttributeTypeMismatch", $"Attribute '{attrName}' already defined with type '{NodeAttributeDefinitionManager.IndexToType[attrIndex]}', cannot set values of type '{nodeAttributeType}'.");
            }
            foreach ((uint nodeId, string attrValueStr) in attrDict)
                SetNodeAttribute(nodeId, attrName, attrValueStr);
            Modified();
            return OperationResult.Ok($"Node attribute '{attrName}' set for {attrDict.Count} nodes in nodeset '{Name}'.");
        }

        /// <summary>
        /// Given an array of node ids, creates a new array of node ids that only includes each
        /// node id once and where the node id is part of this Nodeset object.
        /// </summary>
        /// <param name="uints">The array of node ids to check.</param>
        /// <returns>An array of unique node ids that are also part of this Nodeset.</returns>
        internal uint[] FilterOutNonExistingNodeIds(uint[] uints)
        {
            HashSet<uint> existing = new();
            foreach (uint nodeId in uints)
                if (CheckThatNodeExists(nodeId))
                    existing.Add(nodeId);
            return existing.ToArray();
        }
        #endregion


        #region Private methods
        /// <summary>
        /// Support function for new nodeCache to get all uints
        /// </summary>
        /// <returns></returns>
        private uint[] GetAllNodeIds()
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
        private void Modified()
        {
            IsModified = true;
            _nodeIdCache = null;

        }

        /// <summary>
        /// Initialize the capacity of the Hashset storing nodes without attributes
        /// </summary>
        /// <param name="nbrNodesWithoutAttributes">Upper range of number of nodes</param>
        internal void _initSizeWithoutAttributes(int nbrNodesWithoutAttributes)
        {
            _nodesWithoutAttributes = new(nbrNodesWithoutAttributes);
        }

        /// <summary>
        /// Initialize the capacity of the Díctionary storing nodes wit attributes
        /// </summary>
        /// <param name="nbrNodesWithAttributes">Upper range of number of nodes</param>
        internal void _initSizeWithAttributes(int nbrNodesWithAttributes)
        {
            _nodesWithAttributes = new(nbrNodesWithAttributes);
        }


        #endregion
    }
}
