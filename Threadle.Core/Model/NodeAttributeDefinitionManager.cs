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
        private readonly Stack<uint> _recycledIndices = new();

        private readonly Dictionary<string, uint> _nameToIndex = new();
        private readonly Dictionary<uint, string> _indexToName = new();
        private readonly Dictionary<uint, NodeAttributeType> _indexToType = new();

        public IReadOnlyDictionary<string, uint> NameToIndex => _nameToIndex;
        public IReadOnlyDictionary<uint, string> IndexToName => _indexToName;
        public IReadOnlyDictionary<uint, NodeAttributeType> IndexToType => _indexToType;

        public IEnumerable<object> Info =>
            _indexToName.Select(kvp => new { Name = kvp.Value, Type = _indexToType[kvp.Key].ToString() });

        private byte _nextIndex = 0;

        public OperationResult<uint> DefineNewNodeAttribute(string attributeName, NodeAttributeType attributeType)
        {
            if (attributeName.Length < 1)
                return OperationResult<uint>.Fail("AttributeNameMissing", "Name of attribute must be at least one character.");
            if (_nameToIndex.ContainsKey(attributeName))
                return OperationResult<uint>.Fail("AttributeNameExists", $"Node attribute named '{attributeName}' already defined");

            // Reuse an index if one is available, otherwise use the next one in sequence
            uint index = _recycledIndices.Count > 0 ? _recycledIndices.Pop() : _nextIndex++;

            _nameToIndex[attributeName] = index;
            _indexToType[index] = attributeType;
            _indexToName[index] = attributeName;
            return OperationResult<uint>.Ok(index, $"Node attribute '{attributeName}' of type {attributeType} defined.");
        }

        public bool TryGetAttributeIndex(string name, out uint index) => _nameToIndex.TryGetValue(name, out index);
        public bool TryGetAttributeType(uint index, out NodeAttributeType type) => _indexToType.TryGetValue(index, out type);
        public IEnumerable<(string Name, NodeAttributeType Type)> GetAllNodeAttributeDefinitions() => _nameToIndex.Select(kvp => (kvp.Key, _indexToType[kvp.Value]));


        

        public NodeAttributeDefinitionManager Clone()
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

        internal OperationResult<uint> UndefineNodeAttribute(string attributeName)
        {
            if (!_nameToIndex.TryGetValue(attributeName, out var attributeIndex))
                return OperationResult<uint>.Fail("AttributeNotFound", $"Node attribute '{attributeName}' not found.");

            _nameToIndex.Remove(attributeName);
            _indexToName.Remove(attributeIndex);
            _indexToType.Remove(attributeIndex);
            _recycledIndices.Push(attributeIndex);

            return OperationResult<uint>.Ok(attributeIndex, $"Node attribute '{attributeName}' is no longer defined.");
        }

        public List<Dictionary<string, object>> GetMetadataList()
        {
            return _nameToIndex.Select(kvp => new Dictionary<string,object>
            {
                ["Name"] = kvp.Key,
                ["Type"] = _indexToType[kvp.Value].ToString()
            }).ToList();
        }


        //internal IReadOnlyList<NodeAttributeMetadata> GetMetadataList()
        //{
        //    return _nameToIndex
        //        .Select(kvp => new NodeAttributeMetadata(kvp.Key, _indexToType[kvp.Value]))
        //        .ToList();
        //}
    }
}
