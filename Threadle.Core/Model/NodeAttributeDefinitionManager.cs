using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

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
        /// Defines a new node attribute with the given name and <see cref="NodeAttributeType"/> type.
        /// </summary>
        /// <param name="attributeName">The name of the node attribute.</param>
        /// <param name="attributeType">The <see cref="NodeAttributeType"/> of the node attribute.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, including the node attribute index if successful.</returns>
        internal OperationResult<byte> DefineNewNodeAttribute(string attributeName, NodeAttributeType attributeType)
        {
            if (attributeName.Length < 1)
                return OperationResult<byte>.Fail("MissingAttributeName", "Name of attribute must be at least one character.");
            if (_nameToIndex.ContainsKey(attributeName))
                return OperationResult<byte>.Fail("AttributeAlreadyExists", $"Node attribute named '{attributeName}' already defined");
            byte index = _recycledIndices.Count > 0 ? _recycledIndices.Pop() : _nextIndex++;
            _nameToIndex[attributeName] = index;
            _indexToType[index] = attributeType;
            _indexToName[index] = attributeName;
            return OperationResult<byte>.Ok(index, $"Node attribute '{attributeName}' of type {attributeType} defined.");
        }

        /// <summary>
        /// Tries to get the node attribute index of the specified node attribute.
        /// </summary>
        /// <param name="name">The name of the node attribute.</param>
        /// <param name="index">The (outbound) index of the attribute.</param>
        /// <returns>Returns true if the node attribute was found, false otherwise.</returns>
        internal bool TryGetAttributeIndex(string name, out byte index) => _nameToIndex.TryGetValue(name, out index);

        /// <summary>
        /// Tries to get the node attribute type of the node attribute at the specified node attribute index.
        /// </summary>
        /// <param name="index">The node attribute index.</param>
        /// <param name="type">The (outbound) <see cref="NodeAttributeType"/> of the node attribute.</param>
        /// <returns>Returns true if the node attribute was found, false otherwise.</returns>
        internal bool TryGetAttributeType(byte index, out NodeAttributeType type) => _indexToType.TryGetValue(index, out type);

        internal bool TryGetAttributeName(byte index, out string name) => _indexToName.TryGetValue(index, out name!);

        /// <summary>
        /// Returns a collection of tuples of node attribute names and <see cref="NodeAttributeType"/> values for all defined node attributes.
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<(byte Index, string Name, NodeAttributeType Type)> GetAllNodeAttributeDefinitions() => _nameToIndex.Select(kvp => (kvp.Value, kvp.Key, _indexToType[kvp.Value]));
        
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
        /// <param name="attributeName">The name of the node attribute to discard (undefine).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went, including the index of the node attribute that was now removed.</returns>
        internal OperationResult<byte> UndefineNodeAttribute(string attributeName)
        {
            if (!_nameToIndex.TryGetValue(attributeName, out byte attributeIndex))
                return OperationResult<byte>.Fail("AttributeNotFound", $"Node attribute '{attributeName}' not found.");
            _nameToIndex.Remove(attributeName);
            _indexToName.Remove(attributeIndex);
            _indexToType.Remove(attributeIndex);
            _recycledIndices.Push(attributeIndex);
            return OperationResult<byte>.Ok(attributeIndex, $"Node attribute '{attributeName}' is no longer defined.");
        }

        /// <summary>
        /// Returns a dictionary of information about the defined node attributes
        /// </summary>
        /// <returns>A dictionary with details about the defined node attributes.</returns>
        internal List<Dictionary<string, object>> GetMetadataList()
        {
            return _nameToIndex.Select(kvp => new Dictionary<string,object>
            {
                ["Name"] = kvp.Key,
                ["Type"] = _indexToType[kvp.Value].ToString()
            }).ToList();
        }
        #endregion
    }
}
