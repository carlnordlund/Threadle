using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Helper class used by the layer importers to translate node identifiers found in imported files
    /// into Threadle node ids.
    /// In the default (numeric) mode, identifiers must be unsigned integers that are used as node ids as-is.
    /// In label mode, identifiers are arbitrary strings that are matched against a string node attribute
    /// (the label attribute): known labels map to their existing node id, whereas unknown labels are given
    /// new node ids (continuing after the highest existing node id), with the label stored in the label attribute.
    /// </summary>
    internal sealed class NodeIdResolver
    {
        #region Fields
        /// <summary>
        /// The Nodeset that node ids are resolved against.
        /// </summary>
        private readonly Nodeset _nodeset;

        /// <summary>
        /// Whether nodes not in the Nodeset should be added (true) or ignored (false).
        /// </summary>
        private readonly bool _addMissingNodes;

        /// <summary>
        /// Lookup from node label to node id (only used in label mode).
        /// </summary>
        private readonly Dictionary<string, uint>? _labelToId;

        /// <summary>
        /// The attribute index of the label attribute (only used in label mode).
        /// </summary>
        private readonly byte _labelAttrIndex;

        /// <summary>
        /// The next node id to try when creating new nodes in label mode.
        /// </summary>
        private uint _nextId;
        #endregion


        #region Constructors
        private NodeIdResolver(Nodeset nodeset, bool addMissingNodes, Dictionary<string, uint>? labelToId, byte labelAttrIndex, uint nextId)
        {
            _nodeset = nodeset;
            _addMissingNodes = addMissingNodes;
            _labelToId = labelToId;
            _labelAttrIndex = labelAttrIndex;
            _nextId = nextId;
        }
        #endregion


        #region Properties
        /// <summary>
        /// Returns true if identifiers are treated as string labels, false if they are numeric node ids.
        /// </summary>
        internal bool IsLabelMode => _labelToId != null;

        /// <summary>
        /// Returns true if nodes not found in the Nodeset are added to it, false if they are ignored.
        /// </summary>
        internal bool AddMissingNodes => _addMissingNodes;

        /// <summary>
        /// Number of nodes that have been added to the Nodeset by this resolver.
        /// </summary>
        internal int NbrNodesAdded { get; private set; }
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Creates a NodeIdResolver for the given Nodeset. If <paramref name="labelAttr"/> is null or empty, the
        /// resolver works in numeric mode. Otherwise it works in label mode, using the string node attribute with that
        /// name to store and look up node labels. If the attribute does not exist, it is defined (as a string attribute).
        /// </summary>
        /// <param name="nodeset">The Nodeset to resolve node ids against.</param>
        /// <param name="labelAttr">The name of the string node attribute holding node labels (or null for numeric mode).</param>
        /// <param name="addMissingNodes">Whether nodes not found in the Nodeset should be added to it.</param>
        /// <returns>The NodeIdResolver.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the label attribute exists but is not a string attribute,
        /// or if several existing nodes share the same label.</exception>
        internal static NodeIdResolver Create(Nodeset nodeset, string? labelAttr, bool addMissingNodes)
        {
            if (string.IsNullOrEmpty(labelAttr))
                return new NodeIdResolver(nodeset, addMissingNodes, null, 0, 0);

            byte attrIndex;
            var definition = nodeset.NodeAttributeDefinitionManager.GetNodeAttributeDefinition(labelAttr);
            if (definition is null)
            {
                var defineResult = nodeset.DefineNodeAttribute(labelAttr, NodeAttributeType.String);
                if (!defineResult.Success)
                    throw new InvalidOperationException(defineResult.Message);
                attrIndex = defineResult.Value;
            }
            else
            {
                if (definition.Value.AttrType != NodeAttributeType.String)
                    throw new InvalidOperationException($"Label attribute '{labelAttr}' exists but is of type '{definition.Value.AttrType}': it must be a string attribute.");
                attrIndex = definition.Value.Index;
            }

            // Build label lookup from existing nodes, and find the first free node id
            var labelToId = new Dictionary<string, uint>();
            uint nextId = 0;
            foreach (uint nodeId in nodeset.NodeIdArray)
            {
                if (nodeId >= nextId)
                    nextId = nodeId + 1;
                if (nodeset.GetNodeAttribute(nodeId, attrIndex) is NodeAttributeValue value)
                {
                    string label = nodeset.GetStringFromPool(value.RawValueAsInt());
                    if (!labelToId.TryAdd(label, nodeId))
                        throw new InvalidOperationException($"Label '{label}' in attribute '{labelAttr}' is shared by nodes {labelToId[label]} and {nodeId}: labels must be unique.");
                }
            }
            return new NodeIdResolver(nodeset, addMissingNodes, labelToId, attrIndex, nextId);
        }

        /// <summary>
        /// Checks whether a node identifier (as found in a file) is well-formed, without resolving it or adding
        /// any node: in numeric mode it must be an unsigned integer, in label mode it must be a non-empty string.
        /// </summary>
        /// <param name="token">The node identifier as found in the file.</param>
        /// <returns>True if the identifier is well-formed, false otherwise.</returns>
        internal bool IsValidToken(string token)
        {
            if (_labelToId == null)
                return uint.TryParse(Misc.TrimQuotes(token), out _);
            return Misc.TrimQuotes(token.Trim()).Length > 0;
        }

        /// <summary>
        /// Tries to resolve a node identifier (as found in a file) to a node id in the Nodeset. Surrounding
        /// quotes and whitespace are removed. If the node is not found, it is either added (if missing nodes
        /// should be added) or the method returns false.
        /// </summary>
        /// <param name="token">The node identifier as found in the file.</param>
        /// <param name="nodeId">The resolved node id.</param>
        /// <returns>True if the identifier could be resolved to a node in the Nodeset, false otherwise.</returns>
        internal bool TryResolve(string token, out uint nodeId)
        {
            if (_labelToId == null)
            {
                if (!uint.TryParse(Misc.TrimQuotes(token), out nodeId))
                    return false;
                if (_nodeset.Contains(nodeId))
                    return true;
                if (!_addMissingNodes)
                    return false;
                _nodeset._addNodeWithoutAttribute(nodeId);
                NbrNodesAdded++;
                return true;
            }

            string label = Misc.TrimQuotes(token.Trim());
            if (label.Length == 0)
            {
                nodeId = 0;
                return false;
            }
            if (_labelToId.TryGetValue(label, out nodeId))
                return true;
            if (!_addMissingNodes)
                return false;
            while (_nodeset.Contains(_nextId))
                _nextId++;
            nodeId = _nextId++;
            _nodeset._addNodeWithoutAttribute(nodeId);
            _nodeset.SetNodeAttribute(nodeId, _labelAttrIndex, new NodeAttributeValue(_nodeset.GetOrAddStringToPool(label)));
            _labelToId[label] = nodeId;
            NbrNodesAdded++;
            return true;
        }
        #endregion
    }
}
