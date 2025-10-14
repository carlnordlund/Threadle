using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Manager for a set of Node attributes. Several Nodesets can share the same manager instance,
    /// which is what happens when one is creating a subset of an existing Nodeset.
    /// </summary>
    public class NodeAttributeDefinitionManager
    {
        private readonly Dictionary<string, byte> _nameToIndex = new();
        private readonly Dictionary<byte, string> _indexToName = new();
        private readonly Dictionary<byte, NodeAttributeType> _indexToType = new();

        public IReadOnlyDictionary<string, byte> NameToIndex => _nameToIndex;
        public IReadOnlyDictionary<byte, string> IndexToName => _indexToName;
        public IReadOnlyDictionary<byte, NodeAttributeType> IndexToType => _indexToType;

        private byte _nextIndex = 0;

        public OperationResult DefineNewNodeAttribute(string attributeName, NodeAttributeType attributeType)
        {
            if (_nextIndex == byte.MaxValue)
                return OperationResult.Fail("MaxNbrAttributesReached", $"Max number of attributes (255) reached!");
            if (attributeName.Length < 1)
                return OperationResult.Fail("AttributeNameMissing", "Name of attribute must be at least one character.");
            if (_nameToIndex.ContainsKey(attributeName))
                return OperationResult.Fail("AttributeNameExists", $"Node attribute named '{attributeName}' already defined");

            byte id = _nextIndex++;
            _nameToIndex[attributeName] = id;
            _indexToType[id] = attributeType;
            _indexToName[id] = attributeName;
            return OperationResult.Ok($"Node attribute '{attributeName}' of type {attributeType} defined.");
        }

        public bool TryGetAttributeIndex(string name, out byte index) => _nameToIndex.TryGetValue(name, out index);
        public bool TryGetAttributeType(byte index, out NodeAttributeType type) => _indexToType.TryGetValue(index, out type);
        public IEnumerable<(string Name, NodeAttributeType Type)> GetAllDefinitions() => _nameToIndex.Select(kvp => (kvp.Key, _indexToType[kvp.Value]));

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
    }
}
