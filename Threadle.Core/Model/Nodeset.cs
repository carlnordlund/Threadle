using System.Buffers.Binary;
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
        /// Maps each node id to its slot: a compact, internal position used to index the attribute storage.
        /// This is the single source of node membership. Slots of removed nodes are recycled.
        /// </summary>
        private Dictionary<uint, int> _slotOf = new();

        /// <summary>
        /// The next never-used slot (i.e. the slot high-water mark).
        /// </summary>
        private int _nextSlot = 0;

        /// <summary>
        /// Slots freed by removed nodes, available for reuse. A freed slot is always fully cleared.
        /// </summary>
        private readonly Stack<int> _freeSlots = new();

        /// <summary>
        /// The number of slots that the per-slot storage arrays (sparse blobs and dense columns) have room for.
        /// </summary>
        private int _slotCapacity = 0;

        /// <summary>
        /// Storage for sparse node attributes, indexed by slot. Lazy-initialized when the first sparse value is set.
        /// Each entry is null (no sparse attributes for that node) or a compact blob holding k attributes:
        /// k attribute index bytes followed by k 4-byte (little-endian) raw values, i.e. the blob length is 5*k.
        /// </summary>
        private byte[]?[]? _sparse;

        /// <summary>
        /// Storage for dense node attributes, indexed by attribute index. An entry is null if the attribute
        /// is not dense (or is dense but has no values yet: columns are created when the first value is set).
        /// </summary>
        private readonly DenseColumn?[] _dense = new DenseColumn?[256];

        /// <summary>
        /// Internal array storing an array of all nodeId uint values. Lazy-initialized by NodeIdArray
        /// </summary>
        private uint[]? _nodeIdCache;

        /// <summary>
        /// Pool of unique string values for string-typed node attributes. The pool index is stored
        /// as the IntValue of the corresponding NodeAttributeValue.
        /// </summary>
        private List<string> _stringPool = [];

        /// <summary>
        /// Reverse lookup from string value to pool index, for O(1) deduplication.
        /// </summary>
        private Dictionary<string, int> _stringPoolLookup = [];

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
            EnsureNodeCapacity(createNodes);
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
            EnsureNodeCapacity(nodeIds.Count);
            foreach (uint nodeId in nodeIds)
                _addNodeWithoutAttribute(nodeId);
            IsModified = false;
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
                    foreach (var (attrIndex, attrValue) in GetNodeAttributePairs(nodeId))
                    {
                        NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType);
                        string displayValue = attrType == NodeAttributeType.String
                            ? GetStringFromPool((int)attrValue.GetValue(attrType)!)
                            : attrValue.ToString(attrType);
                        lines.Add($" {NodeAttributeDefinitionManager.IndexToName[attrIndex]}: {displayValue}");
                    }
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
        /// Returns the number of nodes in this Nodeset.
        /// </summary>
        public int Count => _slotOf.Count;

        /// <summary>
        /// Exposes the string pool for binary serialization.
        /// </summary>
        public IReadOnlyList<string> StringPool => _stringPool;
        #endregion


        #region Methods (public)
        /// <summary>
        /// Merges two nodesets into a new one: the union of their node ids, and the union of their
        /// defined node attributes. A node attribute name defined in both sources must have the same
        /// <see cref="NodeAttributeType"/> in both — a type mismatch always fails the merge, since that
        /// signals two genuinely different attributes sharing a name by accident. Values are then
        /// resolved per node and attribute: if only one source has a value set, that value is used; if
        /// both have a value set and they agree, that value is used; if both have a value set and they
        /// disagree, <paramref name="onValueConflict"/> decides. A node missing a value in one source is
        /// never treated as a conflict. Attributes expected to vary between the two sources (e.g. a
        /// per-period measure like income) should be renamed with their period beforehand, so that only
        /// genuinely static attributes (e.g. gender, birthyear) remain under a shared name.
        /// </summary>
        /// <param name="nodeset1">The first nodeset to merge.</param>
        /// <param name="nodeset2">The second nodeset to merge.</param>
        /// <param name="name">The name for the new, merged nodeset.</param>
        /// <param name="onValueConflict">How to resolve a node attribute value set differently in both sources.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the merged Nodeset.</returns>
        public static OperationResult<Nodeset> Merge(Nodeset nodeset1, Nodeset nodeset2, string name, AttributeConflictPolicy onValueConflict = AttributeConflictPolicy.Fail)
        {
            List<string> names1 = nodeset1.GetDefinedAttributeNames();
            List<string> names2 = nodeset2.GetDefinedAttributeNames();
            List<string> allNames = new(names1);
            foreach (string attrName in names2)
                if (!allNames.Contains(attrName))
                    allNames.Add(attrName);

            var attrTypes = new Dictionary<string, NodeAttributeType>();
            var attrStorages = new Dictionary<string, NodeAttributeStorage>();
            foreach (string attrName in allNames)
            {
                var def1 = nodeset1.NodeAttributeDefinitionManager.GetNodeAttributeDefinition(attrName);
                var def2 = nodeset2.NodeAttributeDefinitionManager.GetNodeAttributeDefinition(attrName);
                if (def1 is not null && def2 is not null && def1.Value.AttrType != def2.Value.AttrType)
                    return OperationResult<Nodeset>.Fail("AttributeTypeMismatch", $"Attribute '{attrName}' is defined as '{def1.Value.AttrType}' in nodeset '{nodeset1.Name}' but '{def2.Value.AttrType}' in nodeset '{nodeset2.Name}'; rename one before merging.");
                attrTypes[attrName] = def1?.AttrType ?? def2!.Value.AttrType;
                // Dense if dense in either source
                attrStorages[attrName] = (def1?.Storage == NodeAttributeStorage.Dense || def2?.Storage == NodeAttributeStorage.Dense)
                    ? NodeAttributeStorage.Dense
                    : NodeAttributeStorage.Sparse;
            }

            HashSet<uint> allIds = new(nodeset1.NodeIdArray);
            allIds.UnionWith(nodeset2.NodeIdArray);
            Nodeset merged = new(name, allIds);

            var mergedIndex = new Dictionary<string, byte>();
            foreach (string attrName in allNames)
            {
                OperationResult<byte> defineResult = merged.DefineNodeAttribute(attrName, attrTypes[attrName], attrStorages[attrName]);
                if (!defineResult.Success)
                    return OperationResult<Nodeset>.Fail(defineResult);
                mergedIndex[attrName] = defineResult.Value;
            }

            foreach (uint id in allIds)
            {
                foreach (string attrName in allNames)
                {
                    NodeAttributeType type = attrTypes[attrName];
                    var result1 = nodeset1.GetNodeAttribute(id, attrName);
                    var result2 = nodeset2.GetNodeAttribute(id, attrName);
                    bool has1 = result1.Success;
                    bool has2 = result2.Success;
                    if (!has1 && !has2)
                        continue;

                    string? str1 = null, str2 = null;
                    if (type == NodeAttributeType.String)
                    {
                        if (has1)
                            str1 = nodeset1.GetStringFromPool((int)result1.Value.Value.GetValue(type)!);
                        if (has2)
                            str2 = nodeset2.GetStringFromPool((int)result2.Value.Value.GetValue(type)!);
                    }

                    bool useFirst;
                    if (has1 && has2)
                    {
                        // Float must compare by numeric value, not raw bit pattern: 0.0 and -0.0
                        // have different bits but are the same value and must not be flagged as
                        // a conflict. Int/Bool/Char have no such distinct-bits-same-value case, so
                        // the cheaper bitwise comparison is still used for them.
                        bool equal = type switch
                        {
                            NodeAttributeType.String => str1 == str2,
                            NodeAttributeType.Float => (float)result1.Value.Value.GetValue(type)! == (float)result2.Value.Value.GetValue(type)!,
                            _ => result1.Value.Value.RawValueAsInt() == result2.Value.Value.RawValueAsInt()
                        };
                        if (equal)
                            useFirst = true;
                        else
                            switch (onValueConflict)
                            {
                                case AttributeConflictPolicy.KeepFirst:
                                    useFirst = true;
                                    break;
                                case AttributeConflictPolicy.KeepLast:
                                    useFirst = false;
                                    break;
                                default:
                                    return OperationResult<Nodeset>.Fail("AttributeValueConflict", $"Node {id} has conflicting values for attribute '{attrName}': '{(type == NodeAttributeType.String ? str1 : result1.Value.Value.ToString(type))}' in '{nodeset1.Name}' vs '{(type == NodeAttributeType.String ? str2 : result2.Value.Value.ToString(type))}' in '{nodeset2.Name}'.");
                            }
                    }
                    else
                        useFirst = has1;

                    NodeAttributeValue finalValue = type == NodeAttributeType.String
                        ? new NodeAttributeValue(merged.GetOrAddStringToPool((useFirst ? str1 : str2)!))
                        : (useFirst ? result1.Value.Value : result2.Value.Value);
                    merged.SetNodeAttribute(id, mergedIndex[attrName], finalValue);
                }
            }

            return OperationResult<Nodeset>.Ok(merged, $"Merged nodesets '{nodeset1.Name}' and '{nodeset2.Name}' into '{name}': {allIds.Count} node(s), {allNames.Count} attribute(s).");
        }

        /// <summary>
        /// Adds a node with the specified nodeid to this Nodeset. Checks that it doesn't already exist.
        /// Initially the node has no attributes.
        /// </summary>
        /// <param name="nodeId">The id of the node that is to be added.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult AddNode(uint nodeId)
        {
            if (Contains(nodeId))
                return OperationResult.Fail("NodeAlreadyExists", $"Node with ID '{nodeId}' already exists in nodeset '{Name}'.");
            _addNodeWithoutAttribute(nodeId);
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
            if (_slotOf.Remove(nodeId, out int slot))
            {
                // Clear all attribute storage for this slot before recycling it
                if (_sparse != null)
                    _sparse[slot] = null;
                foreach (DenseColumn? column in _dense)
                    column?.Clear(slot);
                _freeSlots.Push(slot);
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
        /// <param name="attrTypeStr">The textual representation of this node attribute type (either 'char','int','float','bool', or 'string').</param>
        /// <param name="storageStr">The textual representation of the storage mode (either 'sparse' or 'dense'). Defaults to 'sparse'.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult DefineNodeAttribute(string attrName, string attrTypeStr, string storageStr = "sparse")
        {
            if (!(Misc.GetAttributeType(attrTypeStr) is NodeAttributeType attrType))
                return OperationResult.Fail("InvalidAttributeType", $"Attribute type '{attrTypeStr}' not recognized.");
            if (!(Misc.GetAttributeStorage(storageStr) is NodeAttributeStorage storage))
                return OperationResult.Fail("InvalidAttributeStorage", $"Attribute storage '{storageStr}' not recognized: must be 'sparse' or 'dense'.");
            return DefineNodeAttribute(attrName, attrType, storage);
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

            // Remove this attribute from all nodes: drop the dense column (if any) and strip it from all sparse blobs
            byte oldAttrIndex = result.Value;
            _dense[oldAttrIndex] = null;
            if (_sparse != null)
                for (int slot = 0; slot < _nextSlot; slot++)
                    if (_sparse[slot] != null)
                        _removeSparse(slot, oldAttrIndex);
            _modified();
            return OperationResult.Ok($"Node attribute '{attrName}' is no longer defined.");
        }

        /// <summary>
        /// Renames a defined node attribute. The attribute's index and all currently set values are
        /// unaffected — only the name it is looked up by changes.
        /// </summary>
        /// <param name="from">The current name of the node attribute.</param>
        /// <param name="to">The new name for the node attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult RenameNodeAttribute(string from, string to)
        {
            OperationResult result = NodeAttributeDefinitionManager.RenameNodeAttribute(from, to);
            if (result.Success)
                _modified();
            return result;
        }

        /// <summary>
        /// Duplicates a node attribute under a new name, copying its type and all currently set
        /// values for every node that has it set. The original attribute is unaffected.
        /// </summary>
        /// <param name="from">The current name of the node attribute to duplicate.</param>
        /// <param name="to">The name for the new, duplicated node attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public OperationResult DuplicateNodeAttribute(string from, string to)
        {
            var definition = NodeAttributeDefinitionManager.GetNodeAttributeDefinition(from);
            if (definition is null)
                return OperationResult.Fail("AttributeUnknown", $"Node attribute '{from}' not found.");
            byte fromIndex = definition.Value.Index;
            NodeAttributeType attrType = definition.Value.AttrType;
            NodeAttributeStorage storage = definition.Value.Storage;
            to = to.Trim();
            if (to.Length < 1)
                return OperationResult.Fail("MissingAttributeName", "New name of attribute must be at least one character.");
            if (NodeAttributeDefinitionManager.CheckIfAttributeNameExists(to))
                return OperationResult.Fail("AttributeAlreadyExists", $"Node attribute named '{to}' already defined.");

            OperationResult<byte> defineResult = DefineNodeAttribute(to, attrType, storage);
            if (!defineResult.Success)
                return defineResult;
            byte toIndex = defineResult.Value;

            foreach (int slot in _slotOf.Values)
                if (_tryGetBySlot(slot, fromIndex, out NodeAttributeValue value))
                    _setBySlot(slot, toIndex, value);
            _modified();
            return OperationResult.Ok($"Node attribute '{from}' duplicated as '{to}'.");
        }

        /// <summary>
        /// Converts a node attribute to a new type, under a new name: the original attribute is
        /// never touched or removed. Each node's value is converted independently by formatting it
        /// as a string (the same formatting used when exporting or displaying it) and re-parsing
        /// that string as the new type, using the exact same parser <see cref="SetNodeAttribute(uint, string, string)"/>
        /// already uses for CLI string arguments. A value that doesn't parse as the new type - e.g.
        /// a non-numeric string converted to Int, or a multi-character string converted to Char -
        /// is simply left unset on the new attribute for that node, same as a node that had no value
        /// for the source attribute at all; nothing else aborts because of it. Converting a String to
        /// Float or Int keeps that parser's native handling of "NaN"/"Infinity" as real values, not
        /// as missing data.
        /// </summary>
        /// <param name="from">The current name of the node attribute to convert.</param>
        /// <param name="to">The name for the new, converted node attribute.</param>
        /// <param name="newTypeStr">The type to convert to ('int', 'char', 'float', 'bool', or 'string').</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, including how many
        /// node values converted versus were left unset.</returns>
        public OperationResult ConvertNodeAttributeType(string from, string to, string newTypeStr)
        {
            if (!(Misc.GetAttributeType(newTypeStr) is NodeAttributeType newType))
                return OperationResult.Fail("InvalidAttributeType", $"Attribute type '{newTypeStr}' not recognized.");

            var definition = NodeAttributeDefinitionManager.GetNodeAttributeDefinition(from);
            if (definition is null)
                return OperationResult.Fail("AttributeUnknown", $"Node attribute '{from}' not found.");
            byte fromIndex = definition.Value.Index;
            NodeAttributeType fromType = definition.Value.AttrType;
            NodeAttributeStorage storage = definition.Value.Storage;
            to = to.Trim();
            if (to.Length < 1)
                return OperationResult.Fail("MissingAttributeName", "New name of attribute must be at least one character.");
            if (NodeAttributeDefinitionManager.CheckIfAttributeNameExists(to))
                return OperationResult.Fail("AttributeAlreadyExists", $"Node attribute named '{to}' already defined.");

            OperationResult<byte> defineResult = DefineNodeAttribute(to, newType, storage);
            if (!defineResult.Success)
                return defineResult;
            byte toIndex = defineResult.Value;

            int converted = 0, skipped = 0;
            foreach (int slot in _slotOf.Values)
            {
                if (!_tryGetBySlot(slot, fromIndex, out NodeAttributeValue oldValue))
                    continue;
                string str = fromType == NodeAttributeType.String
                    ? GetStringFromPool((int)oldValue.GetValue(fromType)!)
                    : oldValue.ToString(fromType);

                NodeAttributeValue? newValue = newType == NodeAttributeType.String
                    ? new NodeAttributeValue(GetOrAddStringToPool(str))
                    : Misc.CreateNodeAttributeValueFromTypeAndString(newType, str);

                if (newValue is NodeAttributeValue nav)
                {
                    _setBySlot(slot, toIndex, nav);
                    converted++;
                }
                else
                    skipped++;
            }
            _modified();
            return OperationResult.Ok($"Node attribute '{from}' ({fromType}) converted to '{to}' ({newType}): {converted} converted, {skipped} skipped.");
        }

        /// <summary>
        /// Returns the names of all currently defined node attributes, in definition (index) order.
        /// </summary>
        /// <returns>A list of node attribute names.</returns>
        public List<string> GetDefinedAttributeNames()
            => NodeAttributeDefinitionManager.GetAllNodeAttributeDefinitions()
                .OrderBy(d => d.Index)
                .Select(d => d.AttrName)
                .ToList();

        /// <summary>
        /// Returns a table of attribute values for the given nodes and attribute names, one column per
        /// attribute, each an array parallel to <paramref name="nodeIds"/>. Used by getattrstable() as the
        /// bulk-export counterpart to importnodeattributes(). A node lacking a value for an attribute (or
        /// not present in the nodeset at all) gets a null entry for that attribute, rather than failing.
        /// </summary>
        /// <param name="nodeIds">The node ids to include, one row per id (in the given order).</param>
        /// <param name="attrNames">The attribute names to include as columns, or null for all currently defined attributes.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested columns.</returns>
        public OperationResult<Dictionary<string, object?[]>> GetNodeAttributesTable(uint[] nodeIds, string[]? attrNames)
        {
            string[] names = attrNames ?? GetDefinedAttributeNames().ToArray();
            var defs = new (byte Index, NodeAttributeType Type)[names.Length];
            for (int a = 0; a < names.Length; a++)
            {
                if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(names[a], out byte idx) ||
                    !NodeAttributeDefinitionManager.TryGetAttributeType(idx, out NodeAttributeType type))
                    return OperationResult<Dictionary<string, object?[]>>.Fail("AttributeUnknown", $"Unknown attribute '{names[a]}' in nodeset '{Name}'.");
                defs[a] = (idx, type);
            }

            var columns = new Dictionary<string, object?[]>();
            for (int a = 0; a < names.Length; a++)
                columns[names[a]] = new object?[nodeIds.Length];

            for (int i = 0; i < nodeIds.Length; i++)
            {
                uint nodeId = nodeIds[i];
                for (int a = 0; a < names.Length; a++)
                {
                    NodeAttributeValue? value = GetNodeAttribute(nodeId, defs[a].Index);
                    columns[names[a]][i] = value is NodeAttributeValue nav
                        ? (defs[a].Type == NodeAttributeType.String
                            ? GetStringFromPool((int)nav.GetValue(defs[a].Type)!)
                            : nav.GetValue(defs[a].Type))
                        : null;
                }
            }
            return OperationResult<Dictionary<string, object?[]>>.Ok(columns, $"Returning attribute table for {nodeIds.Length} node(s) and {names.Length} attribute(s).");
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
            if (!Contains(nodeId))
                return OperationResult.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult.Fail("AttributeUnknown", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                return OperationResult.Fail("AttributeTypeNotFound", $"No type found for attribute '{attrName}' in nodeset '{Name}': possibly corrupted.");

            NodeAttributeValue attrValue;

            if (attrType == NodeAttributeType.String)
            {
                attrValue = new NodeAttributeValue(GetOrAddStringToPool(attrValueStr));
            }
            else
            {
                if (!(Misc.CreateNodeAttributeValueFromTypeAndString(attrType, attrValueStr) is NodeAttributeValue parsed))
                    return OperationResult.Fail("ParseAttributeValueError", $"Could not convert string '{attrValueStr}' to type '{attrType}'.");
                attrValue = parsed;

            }


            SetNodeAttribute(nodeId, attrIndex, attrValue);
            return OperationResult.Ok($"Attribute '{attrName}' for node {nodeId} set to {attrValueStr}.");
        }

        /// <summary>
        /// Sets the attribute for multiple nodes, either to one common value (<paramref name="attrValueStr"/>) or
        /// to one value per node (<paramref name="attrValueStrs"/>, same length and order as <paramref name="nodeIds"/>).
        /// If <paramref name="nodeIds"/> is null, the common value is set for all nodes in the nodeset; per-node
        /// values always require <paramref name="nodeIds"/>. A node id may appear more than once: the last value wins.
        /// All or nothing: all nodes and values are checked before anything is written, so any error leaves the
        /// nodeset unchanged.
        /// </summary>
        /// <param name="nodeIds">The node ids, or null for all nodes in the nodeset.</param>
        /// <param name="attrName">The name of the node attribute.</param>
        /// <param name="attrValueStr">The common value, expressed as a string (or null if per-node values are given).</param>
        /// <param name="attrValueStrs">The per-node values, expressed as strings (or null if a common value is given).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, including the number of nodes updated.</returns>
        public OperationResult SetNodeAttributes(uint[]? nodeIds, string attrName, string? attrValueStr, string[]? attrValueStrs)
        {
            if ((attrValueStr == null) == (attrValueStrs == null))
                return OperationResult.Fail("InvalidArguments", "Exactly one of a common value (attrvalue) or per-node values (attrvalues) must be provided.");
            if (attrValueStrs != null && nodeIds == null)
                return OperationResult.Fail("MissingNodes", "Per-node values (attrvalues) require the nodes they apply to (nodes).");
            if (attrValueStrs != null && attrValueStrs.Length != nodeIds!.Length)
                return OperationResult.Fail("LengthMismatch", $"The number of values ({attrValueStrs.Length}) differs from the number of nodes ({nodeIds.Length}).");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult.Fail("AttributeUnknown", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                return OperationResult.Fail("AttributeTypeNotFound", $"No type found for attribute '{attrName}' in nodeset '{Name}': possibly corrupted.");
            nodeIds ??= NodeIdArray;
            foreach (uint nodeId in nodeIds)
                if (!Contains(nodeId))
                    return OperationResult.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");

            // Parse all values before writing anything. String values are only added to the string pool
            // after all checks have passed, so that a failed call leaves the pool unchanged as well.
            string[] valueStrs = attrValueStrs ?? [attrValueStr!];
            NodeAttributeValue[] values = new NodeAttributeValue[valueStrs.Length];
            if (attrType != NodeAttributeType.String)
                for (int i = 0; i < valueStrs.Length; i++)
                {
                    if (Misc.CreateNodeAttributeValueFromTypeAndString(attrType, valueStrs[i]) is not NodeAttributeValue parsed)
                        return OperationResult.Fail("ParseAttributeValueError", $"Could not convert string '{valueStrs[i]}' to type '{attrType}'.");
                    values[i] = parsed;
                }
            else
                for (int i = 0; i < valueStrs.Length; i++)
                    values[i] = new NodeAttributeValue(GetOrAddStringToPool(valueStrs[i]));

            for (int i = 0; i < nodeIds.Length; i++)
                SetNodeAttribute(nodeIds[i], attrIndex, values[attrValueStrs == null ? 0 : i]);
            int nbrNodes = nodeIds.Distinct().Count();
            return OperationResult.Ok($"Attribute '{attrName}' set for {nbrNodes} node(s) in nodeset '{Name}'.");
        }

        /// <summary>
        /// Gets the specific attrValue for the node attribute of the selected node id.
        /// </summary>
        /// <param name="nodeId">The unique id of the node.</param>
        /// <param name="attrName">The name of the node attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, with the requested <see cref="NodeAttributeValue"/>.</returns>
        public OperationResult<(NodeAttributeValue Value, NodeAttributeType Type)> GetNodeAttribute(uint nodeId, string attrName)
        {
            if (!Contains(nodeId))
                return OperationResult<(NodeAttributeValue, NodeAttributeType)>.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult<(NodeAttributeValue, NodeAttributeType)>.Fail("AttributeUnknown", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                return OperationResult<(NodeAttributeValue, NodeAttributeType)>.Fail("AttributeTypeNotFound", $"No type found for attribute '{attrName}' in nodeset '{Name}' - possibly corrupted.");

            if (!(GetNodeAttribute(nodeId, attrIndex) is NodeAttributeValue attrValue))
                return OperationResult<(NodeAttributeValue, NodeAttributeType)>.Fail("AttributeNotSet", $"Attribute '{attrName}' not set for node '{nodeId}' in nodeset '{Name}'.");
            return OperationResult<(NodeAttributeValue, NodeAttributeType)>.Ok((attrValue, attrType));
        }

        /// <summary>
        /// Gets a string node attribute value, resolving the pool index to the actual string.
        /// Use this instead of GetNodeAttribute when the attribute type is String.
        /// </summary>
        public OperationResult<string> GetNodeAttributeString(uint nodeId, string attrName)
        {
            var result = GetNodeAttribute(nodeId, attrName);
            if (!result.Success)
                return OperationResult<string>.Fail(result);
            var (nav, type) = result.Value;
            if (type != NodeAttributeType.String)
                return OperationResult<string>.Fail("AttributeTypeMismatch", $"Attribute '{attrName}' is not of type String.");
            return OperationResult<string>.Ok(GetStringFromPool((int)nav.GetValue(type)!));
        }

        public OperationResult<Dictionary<uint, object?>> GetMultipleNodeAttributes(uint[] nodeIds, string attrName)
        {
            if (!NodeAttributeDefinitionManager.CheckIfAttributeNameExists(attrName))
                return OperationResult<Dictionary<uint, object?>>.Fail("AttributeUnknown", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");

            var result = new Dictionary<uint, object?>();
            foreach (uint nodeId in nodeIds)
            {
                if (!Contains(nodeId))
                    continue;
                var attrResult = GetNodeAttribute(nodeId, attrName);
                if (attrResult.Success)
                {
                    var (nav, type) = attrResult.Value;
                    result[nodeId] = type == NodeAttributeType.String
                        ? GetStringFromPool((int)nav.GetValue(type)!)
                        : nav.GetValue(type);
                    //result[nodeId] = nav.GetValue(type);
                }
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
            if (!Contains(nodeId))
                return OperationResult.Fail("NodeNotFound", $"Node ID '{nodeId}' not found in nodeset '{Name}'.");
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult.Fail("AttributeUnknown", $"Unknown attribute '{attrName}' in nodeset '{Name}'.");
            if (!RemoveNodeAttribute(nodeId, attrIndex))
                return OperationResult.Fail("AttributeNotSet", $"Attribute '{attrName}' not set for node '{nodeId}' in nodeset '{Name}'.");
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
            int total = allNodes.Length;
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

        /// <summary>
        /// Returns an estimate of the number of bytes used by this Nodeset: the node-to-slot map, the sparse
        /// attribute blobs, the dense attribute columns, the node id cache and the string pool.
        /// </summary>
        /// <returns>The estimated number of bytes.</returns>
        public long GetEstimatedBytes()
        {
            int n = _slotOf.Count;
            // _slotOf: Dictionary<uint,int> entries (16 bytes) + buckets (4 bytes), at ~72% load factor
            long bytes = (long)(n / 0.72 + 1) * 20;
            if (_sparse != null)
            {
                // Array of blob references
                bytes += 24 + (long)_sparse.Length * 8;
                // Each blob: 24 bytes array overhead + payload rounded up to 8 bytes
                for (int slot = 0; slot < _nextSlot; slot++)
                    if (_sparse[slot] is byte[] blob)
                        bytes += 24 + ((blob.Length + 7) & ~7);
            }
            foreach (DenseColumn? column in _dense)
                if (column != null)
                    bytes += column.EstimatedBytes;
            if (_nodeIdCache != null)
                bytes += 24 + (long)_nodeIdCache.Length * 4;
            foreach (string str in _stringPool)
                bytes += 22 + 2L * str.Length + 24;
            return bytes;
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
            bool node1exists = Contains(node1Id);
            bool node2exists = Contains(node2Id);
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
        /// <param name="storage">The storage mode (sparse or dense) of this attribute. Defaults to sparse.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, including the internal attribute index of the
        /// added attribute.</returns>
        internal OperationResult<byte> DefineNodeAttribute(string attrName, NodeAttributeType attrType, NodeAttributeStorage storage = NodeAttributeStorage.Sparse)
        {
            OperationResult<byte> result = NodeAttributeDefinitionManager.DefineNewNodeAttribute(attrName, attrType, storage);
            if (result.Success)
                _modified();
            return result;
        }

        /// <summary>
        /// Defines and sets node attribute values for multiple nodes based on the provided dictionary of nodeId=>attributeValueStr.
        /// If the attribute is already defined, it checks that the provided type matches the existing type. If it exists but with a different type,
        /// an error is returned. If the attribute is not yet defined, it is defined based on the provided type, using dense storage (as this
        /// method is used for storing results of analyses, where all nodes get a value). An existing attribute keeps its storage mode.
        /// </summary>
        /// <param name="attrName">The name of the node attribute.</param>
        /// <param name="attrDict">The dictionary of nodeId=>attributeValueStr.</param>
        /// <param name="attrType">The type of the node attribute.</param>
        /// <returns>Returns an <see cref="OperationResult"/> informing how well it went.</returns>
        internal OperationResult DefineAndSetNodeAttributeValues(string attrName, Dictionary<uint, string> attrDict, NodeAttributeType attrType)
        {
            if (!NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
            {
                OperationResult<byte> defineresult = DefineNodeAttribute(attrName, attrType, NodeAttributeStorage.Dense);
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
        /// Adds a node (initially without attributes) to this Nodeset, assigning it a slot.
        /// No verification is done apart from ignoring an already existing node id: to be used with loaders.
        /// </summary>
        /// <param name="nodeId">The node id to add.</param>
        internal void _addNodeWithoutAttribute(uint nodeId)
        {
            if (_slotOf.ContainsKey(nodeId))
                return;
            int slot;
            if (_freeSlots.Count > 0)
                slot = _freeSlots.Pop();
            else
            {
                slot = _nextSlot++;
                if (slot >= _slotCapacity)
                    _growSlotCapacity(slot + 1);
            }
            _slotOf.Add(nodeId, slot);
            _nodeIdCache = null;
        }

        /// <summary>
        /// Adds a node to this Nodeset (if not already present) and copies all of its attribute values
        /// (sparse and dense) from the corresponding node in the source Nodeset. Requires that both Nodesets
        /// use the same attribute indices, e.g. when this Nodeset's <see cref="NodeAttributeDefinitionManager"/> is
        /// a clone of the source's, and that the string pool has been copied in the same order.
        /// </summary>
        /// <param name="source">The source Nodeset.</param>
        /// <param name="nodeId">The node id to copy.</param>
        internal void CopyNodeFrom(Nodeset source, uint nodeId)
        {
            if (!source._slotOf.TryGetValue(nodeId, out int sourceSlot))
                return;
            _addNodeWithoutAttribute(nodeId);
            int slot = _slotOf[nodeId];
            if (source._sparse?[sourceSlot] is byte[] blob)
            {
                _sparse ??= new byte[]?[_slotCapacity];
                _sparse[slot] = (byte[])blob.Clone();
            }
            for (int a = 0; a < 256; a++)
                if (source._dense[a] is DenseColumn sourceColumn && sourceColumn.TryGet(sourceSlot, out NodeAttributeValue value))
                    _getOrCreateDenseColumn((byte)a).Set(slot, value);
        }

        /// <summary>
        /// Checks if the Nodeset contains a node object with the specified id.
        /// </summary>
        /// <param name="nodeId">The id of the node that is to be checked.</param>
        /// <returns>True if the node id exists, false otherwise.</returns>
        internal bool Contains(uint nodeId) => _slotOf.ContainsKey(nodeId);

        /// <summary>
        /// Sets a node attribute for a specific node, given the attribute index and attribute attrValue.
        /// Dense attributes are written to their column; sparse attributes are written to the node's
        /// sparse blob (overwriting an existing value, or adding it). Does nothing if the node doesn't exist.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <param name="attrValue">The <see cref="NodeAttributeValue"/> to set the attribute to.</param>
        internal void SetNodeAttribute(uint nodeId, byte attrIndex, NodeAttributeValue attrValue)
        {
            if (_slotOf.TryGetValue(nodeId, out int slot))
                _setBySlot(slot, attrIndex, attrValue);
        }

        /// <summary>
        /// Gets the specified attribute from a specific node id based on the attribute index.
        /// Returns null if the node does not have this attribute set (or doesn't exist).
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <returns>Returns a <see cref="NodeAttributeValue"/>, or null if attribute is missing.</returns>
        internal NodeAttributeValue? GetNodeAttribute(uint nodeId, byte attrIndex)
        {
            if (_slotOf.TryGetValue(nodeId, out int slot) && _tryGetBySlot(slot, attrIndex, out NodeAttributeValue value))
                return value;
            return null;
        }

        /// <summary>
        /// Removes (unsets) the specified attribute from a node.
        /// </summary>
        /// <param name="nodeId">The node id</param>
        /// <param name="attrIndex">The attribute index</param>
        /// <returns>Returns true if the attribute was set and is now removed, false if it wasn't set (or the node doesn't exist).</returns>
        internal bool RemoveNodeAttribute(uint nodeId, byte attrIndex)
        {
            if (!_slotOf.TryGetValue(nodeId, out int slot))
                return false;
            if (_dense[attrIndex] is DenseColumn column)
                return column.Clear(slot);
            return _removeSparse(slot, attrIndex);
        }

        /// <summary>
        /// Returns all attributes (sparse and dense) that are set for a specific node, as (attribute index, value) pairs.
        /// Sparse attributes come first, then dense attributes in attribute index order. Returns an empty list if the
        /// node has no attributes or doesn't exist.
        /// </summary>
        /// <param name="nodeId">The node id</param>
        /// <returns>A list of (attribute index, value) pairs.</returns>
        internal List<(byte Index, NodeAttributeValue Value)> GetNodeAttributePairs(uint nodeId)
        {
            var pairs = new List<(byte, NodeAttributeValue)>();
            if (!_slotOf.TryGetValue(nodeId, out int slot))
                return pairs;
            pairs.AddRange(GetSparseNodeAttributePairs(nodeId));
            for (int a = 0; a < 256; a++)
                if (_dense[a] is DenseColumn column && column.TryGet(slot, out NodeAttributeValue value))
                    pairs.Add(((byte)a, value));
            return pairs;
        }

        /// <summary>
        /// Returns the sparse attributes set for a specific node, as (attribute index, value) pairs.
        /// Used by the binary writer, which stores dense attributes separately.
        /// </summary>
        /// <param name="nodeId">The node id</param>
        /// <returns>A list of (attribute index, value) pairs (empty if none).</returns>
        internal List<(byte Index, NodeAttributeValue Value)> GetSparseNodeAttributePairs(uint nodeId)
        {
            var pairs = new List<(byte, NodeAttributeValue)>();
            if (_sparse != null && _slotOf.TryGetValue(nodeId, out int slot) && _sparse[slot] is byte[] blob)
            {
                int k = blob.Length / 5;
                for (int i = 0; i < k; i++)
                    pairs.Add((blob[i], NodeAttributeValue.FromRaw(BinaryPrimitives.ReadInt32LittleEndian(blob.AsSpan(k + 4 * i)))));
            }
            return pairs;
        }

        /// <summary>
        /// Sets several sparse attributes of a node at once, replacing any sparse attributes it had.
        /// No verification is done (indices must be unique, sparse, and defined): to be used with loaders.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrIndexes">The attribute indices.</param>
        /// <param name="attrValues">The attribute values (same length and order as the indices).</param>
        internal void _setSparseNodeAttributes(uint nodeId, byte[] attrIndexes, NodeAttributeValue[] attrValues)
        {
            if (!_slotOf.TryGetValue(nodeId, out int slot))
                return;
            int k = attrIndexes.Length;
            if (k == 0)
            {
                if (_sparse != null)
                    _sparse[slot] = null;
                return;
            }
            byte[] blob = new byte[5 * k];
            for (int i = 0; i < k; i++)
            {
                blob[i] = attrIndexes[i];
                BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(k + 4 * i), attrValues[i].RawValueAsInt());
            }
            _sparse ??= new byte[]?[_slotCapacity];
            _sparse[slot] = blob;
        }

        /// <summary>
        /// Makes sure the internal storage has room for at least the given number of nodes,
        /// avoiding repeated re-allocations when the number of nodes is known in advance (e.g. when loading).
        /// </summary>
        /// <param name="nbrNodes">The expected number of nodes.</param>
        internal void EnsureNodeCapacity(int nbrNodes)
        {
            if (nbrNodes <= 0)
                return;
            _slotOf.EnsureCapacity(nbrNodes);
            if (nbrNodes > _slotCapacity)
                _growSlotCapacity(nbrNodes, exact: true);
        }

        /// <summary>
        /// Returns the pool index for the given string, adding it to the pool if not already present.
        /// </summary>
        internal int GetOrAddStringToPool(string value)
        {
            if (_stringPoolLookup.TryGetValue(value, out int index))
                return index;
            index = _stringPool.Count;
            _stringPool.Add(value);
            _stringPoolLookup[value] = index;
            return index;
        }

        /// <summary>
        /// Returns the string at the given pool index.
        /// </summary>
        internal string GetStringFromPool(int index)
            => (index >= 0 && index < _stringPool.Count) ? _stringPool[index] : "(invalid)";
        #endregion


        #region Private methods
        /// <summary>
        /// Support function for new nodeCache to get all nodeIds
        /// </summary>
        /// <returns></returns>
        private uint[] _getAllNodeIds()
        {
            uint[] allNodes = new uint[_slotOf.Count];
            _slotOf.Keys.CopyTo(allNodes, 0);
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
        /// Grows the capacity of all per-slot storage (sparse blob array and dense columns) to hold at least
        /// <paramref name="required"/> slots. Unless <paramref name="exact"/> is set, capacity is doubled to
        /// amortize the cost of growing.
        /// </summary>
        private void _growSlotCapacity(int required, bool exact = false)
        {
            int newCapacity = exact ? required : Math.Max(required, Math.Max(16, _slotCapacity * 2));
            if (_sparse != null)
                Array.Resize(ref _sparse, newCapacity);
            foreach (DenseColumn? column in _dense)
                column?.EnsureCapacity(newCapacity);
            _slotCapacity = newCapacity;
        }

        /// <summary>
        /// Returns the dense column for the given attribute index, creating it if needed.
        /// </summary>
        private DenseColumn _getOrCreateDenseColumn(byte attrIndex)
            => _dense[attrIndex] ??= new DenseColumn(_slotCapacity);

        /// <summary>
        /// Tries to get the value of an attribute at the given slot, from the dense column if the attribute
        /// has one, otherwise from the slot's sparse blob.
        /// </summary>
        private bool _tryGetBySlot(int slot, byte attrIndex, out NodeAttributeValue value)
        {
            if (_dense[attrIndex] is DenseColumn column)
                return column.TryGet(slot, out value);
            if (_sparse?[slot] is byte[] blob)
            {
                int k = blob.Length / 5;
                int i = Array.IndexOf(blob, attrIndex, 0, k);
                if (i >= 0)
                {
                    value = NodeAttributeValue.FromRaw(BinaryPrimitives.ReadInt32LittleEndian(blob.AsSpan(k + 4 * i)));
                    return true;
                }
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Sets the value of an attribute at the given slot. Dense attributes (as given by the
        /// definition manager) are written to their column, which is created on first use; sparse attributes
        /// are written to the slot's blob.
        /// </summary>
        private void _setBySlot(int slot, byte attrIndex, NodeAttributeValue value)
        {
            DenseColumn? column = _dense[attrIndex];
            if (column == null && NodeAttributeDefinitionManager.TryGetAttributeStorage(attrIndex, out NodeAttributeStorage storage) && storage == NodeAttributeStorage.Dense)
                column = _getOrCreateDenseColumn(attrIndex);
            if (column != null)
            {
                column.Set(slot, value);
                return;
            }
            _setSparse(slot, attrIndex, value);
        }

        /// <summary>
        /// Sets a sparse attribute value in the slot's blob: overwrites in place if present, otherwise
        /// re-allocates the blob with room for one more attribute.
        /// </summary>
        private void _setSparse(int slot, byte attrIndex, NodeAttributeValue value)
        {
            _sparse ??= new byte[]?[_slotCapacity];
            int raw = value.RawValueAsInt();
            byte[]? blob = _sparse[slot];
            if (blob == null)
            {
                blob = new byte[5];
                blob[0] = attrIndex;
                BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(1), raw);
                _sparse[slot] = blob;
                return;
            }
            int k = blob.Length / 5;
            int i = Array.IndexOf(blob, attrIndex, 0, k);
            if (i >= 0)
            {
                BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(k + 4 * i), raw);
                return;
            }
            // New layout: [k old indices][new index][k old values][new value]
            byte[] newBlob = new byte[5 * (k + 1)];
            blob.AsSpan(0, k).CopyTo(newBlob);
            newBlob[k] = attrIndex;
            blob.AsSpan(k, 4 * k).CopyTo(newBlob.AsSpan(k + 1));
            BinaryPrimitives.WriteInt32LittleEndian(newBlob.AsSpan(k + 1 + 4 * k), raw);
            _sparse[slot] = newBlob;
        }

        /// <summary>
        /// Removes a sparse attribute from the slot's blob, re-allocating it with room for one attribute less
        /// (or setting it to null if no attributes remain).
        /// </summary>
        /// <returns>True if the attribute was present and is now removed, false otherwise.</returns>
        private bool _removeSparse(int slot, byte attrIndex)
        {
            if (_sparse?[slot] is not byte[] blob)
                return false;
            int k = blob.Length / 5;
            int i = Array.IndexOf(blob, attrIndex, 0, k);
            if (i < 0)
                return false;
            if (k == 1)
            {
                _sparse[slot] = null;
                return true;
            }
            int m = k - 1;
            byte[] newBlob = new byte[5 * m];
            // Indices, skipping i
            blob.AsSpan(0, i).CopyTo(newBlob);
            blob.AsSpan(i + 1, k - i - 1).CopyTo(newBlob.AsSpan(i));
            // Values, skipping i
            blob.AsSpan(k, 4 * i).CopyTo(newBlob.AsSpan(m));
            blob.AsSpan(k + 4 * (i + 1), 4 * (k - i - 1)).CopyTo(newBlob.AsSpan(m + 4 * i));
            _sparse[slot] = newBlob;
            return true;
        }
        #endregion
    }
}