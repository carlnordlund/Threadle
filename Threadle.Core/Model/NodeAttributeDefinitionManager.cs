using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Manager for a set of Node attributes. Several Nodesets can share the same manager instance,
    /// which is what happens when one is creating a subset of an existing Nodeset.
    /// </summary>
    public class NodeAttributeDefinitionManager
    {
        #region Fields
        /// <summary>
        /// To keep track of the next node attribute index.
        /// Note: maximum number of node attributes is 255.
        /// </summary>
        private byte _nextIndex = 0;

        /// <summary>
        /// Collection of internal node attribute index numbers for recycling.
        /// Strictly a memory-optimizing feature.
        /// </summary>
        private readonly Stack<byte> _recycledIndices = new();

        /// <summary>
        /// Dictionary to map node attribute names to their indices.
        /// </summary>
        private readonly Dictionary<string, byte> _nameToIndex = new();

        /// <summary>
        /// Dictionary to map node attribute indices to their names.
        /// </summary>
        private readonly Dictionary<byte, string> _indexToName = new();

        /// <summary>
        /// Dictionary to map node attribute indices to their node attribyte types.
        /// </summary>
        private readonly Dictionary<byte, NodeAttributeType> _indexToType = new();
        #endregion


        #region Properties (internal)
        /// <summary>
        /// Returns the dictionary mapping node attribute indices to node attribute names.
        /// </summary>
        internal IReadOnlyDictionary<byte, string> IndexToName => _indexToName;

        /// <summary>
        /// Returns the dictionary mapping node attribute indices to node attribute types.
        /// </summary>
        internal IReadOnlyDictionary<byte, NodeAttributeType> IndexToType => _indexToType;
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Defines a new node attribute with the given attrName and <see cref="NodeAttributeType"/> attrType.
        /// </summary>
        /// <param attrName="attrName">The attrName of the node attribute.</param>
        /// <param attrName="attrType">The <see cref="NodeAttributeType"/> of the node attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, including the node attribute index if successful.</returns>
        internal OperationResult<byte> DefineNewNodeAttribute(string attrName, NodeAttributeType attrType)
        {
            if (attrName.Length < 1)
                return OperationResult<byte>.Fail("MissingAttributeName", "Name of attribute must be at least one character.");
            if (CheckIfAttributeNameExists(attrName))
                return OperationResult<byte>.Fail("AttributeAlreadyExists", $"Node attribute named '{attrName}' already defined");
            byte index = _recycledIndices.Count > 0 ? _recycledIndices.Pop() : _nextIndex++;
            _nameToIndex[attrName] = index;
            _indexToType[index] = attrType;
            _indexToName[index] = attrName;
            return OperationResult<byte>.Ok(index, $"Node attribute '{attrName}' of attrType {attrType} defined.");
        }

        /// <summary>
        /// Tries to get the node attribute index of the specified node attribute.
        /// </summary>
        /// <param attrName="attrName">The attrName of the node attribute.</param>
        /// <param attrName="index">The (outbound) index of the attribute.</param>
        /// <returns>Returns true if the node attribute was found, false otherwise.</returns>
        internal bool TryGetAttributeIndex(string attrName, out byte index) => _nameToIndex.TryGetValue(attrName, out index);

        /// <summary>
        /// Tries to get the node attribute attrType of the node attribute at the specified node attribute index.
        /// </summary>
        /// <param attrName="index">The node attribute index.</param>
        /// <param attrName="attrType">The (outbound) <see cref="NodeAttributeType"/> of the node attribute.</param>
        /// <returns>Returns true if the node attribute was found, false otherwise.</returns>
        internal bool TryGetAttributeType(byte index, out NodeAttributeType attrType) => _indexToType.TryGetValue(index, out attrType);

        internal bool TryGetAttributeName(byte index, out string attrName) => _indexToName.TryGetValue(index, out attrName!);

        /// <summary>
        /// Returns a collection of tuples of node attribute names and <see cref="NodeAttributeType"/> values for all defined node attributes.
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<(byte Index, string AttrName, NodeAttributeType AttrType)> GetAllNodeAttributeDefinitions() => _nameToIndex.Select(kvp => (kvp.Value, kvp.Key, _indexToType[kvp.Value]));

        /// <summary>
        /// Returns a (deep) clone of this <see cref="NodeAttributeDefinitionManager"/> object.
        /// Used for instance when creating a subset Nodeset where all node attributes should be cloned.
        /// </summary>
        /// <returns>Returns a <see cref="NodeAttributeDefinitionManager"/> object that is a clone of the current object.</returns>
        internal NodeAttributeDefinitionManager Clone()
        {
            NodeAttributeDefinitionManager clone = new NodeAttributeDefinitionManager();
            foreach (var kvp in _nameToIndex)
                clone._nameToIndex[kvp.Key] = kvp.Value;
            foreach (var kvp in _indexToName)
                clone._indexToName[kvp.Key] = kvp.Value;
            foreach (var kvp in _indexToType)
                clone._indexToType[kvp.Key] = kvp.Value;
            return clone;
        }

        /// <summary>
        /// Discards (un-defines) an existing (defined) node attribute.
        /// If successfully removed, the index of the node attribute is placed on the recycling heap.
        /// </summary>
        /// <param attrName="attrName">The attrName of the node attribute to discard (undefine).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, including the index of the node attribute that was now removed.</returns>
        internal OperationResult<byte> UndefineNodeAttribute(string attrName)
        {
            if (!_nameToIndex.TryGetValue(attrName, out byte attrIndex))
                return OperationResult<byte>.Fail("AttributeNotFound", $"Node attribute '{attrName}' not found.");
            _nameToIndex.Remove(attrName);
            _indexToName.Remove(attrIndex);
            _indexToType.Remove(attrIndex);
            _recycledIndices.Push(attrIndex);
            return OperationResult<byte>.Ok(attrIndex, $"Node attribute '{attrName}' is no longer defined.");
        }

        /// <summary>
        /// Returns a dictionary of information about the defined node attributes
        /// </summary>
        /// <returns>A dictionary with details about the defined node attributes.</returns>
        internal List<Dictionary<string, object>> GetMetadataList()
        {
            return _nameToIndex.Select(kvp => new Dictionary<string, object>
            {
                ["Name"] = kvp.Key,
                ["Type"] = _indexToType[kvp.Value].ToString()
            }).ToList();
        }

        /// <summary>
        /// Support method to check if there is an attribute with this attrName
        /// </summary>
        /// <param attrName="attrName">The attribute attrName to test.</param>
        /// <returns>Returns true if there is an attribute with this attrName, false otherwise.</returns>
        internal bool CheckIfAttributeNameExists(string attrName)
        {
            return _nameToIndex.ContainsKey(attrName);
        }
        #endregion
    }
}
