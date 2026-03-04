using Threadle.Core.Model.Enums;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Read-optimized Compressed Sparse Row representation of a 1-mode layer.
    /// Converted from a <see cref="LayerOneMode"/> via <see cref="FromDynamic"/>.
    /// Outbound (or symmetric) edges are stored per ego node.
    /// </summary>
    public class LayerOneModeCSR : ILayer
    {
        #region Fields
        /// <summary>Maps ego node id to its row index in <see cref="_rowPointers"/>.</summary>
        private Dictionary<uint, int> _nodeIdToIndexMapper = [];

        /// <summary>
        /// Row pointers into <see cref="_colIndices"/>. Node at index i has neighbors in
        /// [_rowPointers[i], _rowPointers[i+1]).
        /// </summary>
        private int[] _rowPointers = [0];

        /// <summary>Flat array of alter node IDs, grouped by ego row.</summary>
        private uint[] _colIndices = [];

        /// <summary>Edge values parallel to <see cref="_colIndices"/>. Null for binary layers.</summary>
        private float[]? _values;
        #endregion


        #region Properties
        public string Name { get; set; } = "";
        public EdgeDirectionality Directionality { get; init; }
        public EdgeType EdgeValueType { get; init; }
        public bool Selfties { get; init; }

        public bool IsSymmetric => Directionality == EdgeDirectionality.Undirected;
        public bool IsValued => EdgeValueType == EdgeType.Valued;

        public Dictionary<string, object> GetMetadata => new()
        {
            ["Name"] = Name,
            ["Mode"] = 1,
            ["Directionality"] = Directionality.ToString(),
            ["ValueType"] = EdgeValueType.ToString(),
            ["SelftiesAllowed"] = Selfties,
            ["Format"] = "CSR"
        };

        public string GetLayerInfo =>
            $" {Name} [1-mode CSR: {EdgeValueType},{Directionality},{Selfties}]";
        #endregion


        #region Factory
        /// <summary>
        /// Converts a dynamic <see cref="LayerOneMode"/> to a <see cref="LayerOneModeCSR"/>.
        /// Outbound edges are used for directed layers; all connected edges for symmetric ones.
        /// </summary>
        public static LayerOneModeCSR FromDynamic(LayerOneMode layer)
        {
            var sortedNodeIds = layer.Edgesets.Keys.OrderBy(id => id).ToArray();
            int nNodes = sortedNodeIds.Length;

            var mapper = new Dictionary<uint, int>(nNodes);
            var rowPointers = new int[nNodes + 1];
            var colIndicesList = new List<uint>();
            List<float>? valuesList = layer.IsValued ? [] : null;

            for (int i = 0; i < nNodes; i++)
            {
                uint egoId = sortedNodeIds[i];
                IEdgeset edgeset = layer.Edgesets[egoId];
                mapper[egoId] = i;
                rowPointers[i] = colIndicesList.Count;

                if (layer.IsValued)
                {
                    foreach (var (alterId, value) in edgeset.GetOutboundEdgesWithValues(egoId))
                    {
                        colIndicesList.Add(alterId);
                        valuesList!.Add(value);
                    }
                }
                else
                {
                    colIndicesList.AddRange(edgeset.GetOutboundNodeIds);
                }
            }
            rowPointers[nNodes] = colIndicesList.Count;

            return new LayerOneModeCSR
            {
                Name = layer.Name,
                Directionality = layer.Directionality,
                EdgeValueType = layer.EdgeValueType,
                Selfties = layer.Selfties,
                _nodeIdToIndexMapper = mapper,
                _rowPointers = rowPointers,
                _colIndices = colIndicesList.ToArray(),
                _values = valuesList?.ToArray()
            };
        }
        #endregion


        #region ILayer methods
        public List<string> GetNFirstEdges(int n = 10)
        {
            string arrow = IsSymmetric ? "<->" : "->";
            List<string> edges = new(n);

            foreach (uint egoId in _nodeIdToIndexMapper.Keys)
            {
                if (edges.Count >= n) break;
                int i = _nodeIdToIndexMapper[egoId];
                int start = _rowPointers[i];
                int end = _rowPointers[i + 1];

                for (int j = start; j < end && edges.Count < n; j++)
                {
                    uint alterId = _colIndices[j];
                    if (IsSymmetric && alterId <= egoId)
                        continue;
                    edges.Add(IsValued
                        ? $"{egoId} {arrow} {alterId} ({_values![j]})"
                        : $"{egoId} {arrow} {alterId}");
                }
            }

            return edges;
        }

        public HashSet<uint> GetMentionedNodeIds()
        {
            HashSet<uint> ids = [.. _nodeIdToIndexMapper.Keys];
            foreach (uint alterId in _colIndices)
                ids.Add(alterId);
            return ids;
        }

        public float GetEdgeValue(uint node1Id, uint node2Id)
        {
            if (!_nodeIdToIndexMapper.TryGetValue(node1Id, out int i)) return 0f;
            int start = _rowPointers[i];
            int end = _rowPointers[i + 1];
            int col = Array.BinarySearch(_colIndices, start, end - start, node2Id);
            if (col < 0) return 0f;
            return IsValued ? _values![col] : 1f;
        }

        public bool CheckEdgeExists(uint node1Id, uint node2Id)
        {
            if (!_nodeIdToIndexMapper.TryGetValue(node1Id, out int i)) return false;
            int start = _rowPointers[i];
            int end = _rowPointers[i + 1];
            return Array.BinarySearch(_colIndices, start, end - start, node2Id) >= 0;
        }

        public uint[] GetNodeAlters(uint nodeId, EdgeTraversal edgeTraversal)
        {
            if (!_nodeIdToIndexMapper.TryGetValue(nodeId, out int i)) return [];
            int start = _rowPointers[i];
            int end = _rowPointers[i + 1];
            return _colIndices[start..end];
        }

        /// <summary>
        /// Removes all edges involving <paramref name="nodeId"/> — both its own row as ego
        /// and any references to it as alter in other rows. Rebuilds the CSR arrays.
        /// </summary>
        public void RemoveNodeEdges(uint nodeId)
        {
            var newMapper = new Dictionary<uint, int>(_nodeIdToIndexMapper.Count);
            var newColIndices = new List<uint>(_colIndices.Length);
            List<float>? newValues = IsValued ? new List<float>(_colIndices.Length) : null;

            // Iterate rows in original index order, skipping the removed node
            int newIndex = 0;
            var newRowPointers = new List<int> { 0 };
            foreach (uint egoId in _nodeIdToIndexMapper.Keys.OrderBy(id => _nodeIdToIndexMapper[id]))
            {
                if (egoId == nodeId) continue;
                int oldI = _nodeIdToIndexMapper[egoId];
                newMapper[egoId] = newIndex++;
                for (int j = _rowPointers[oldI]; j < _rowPointers[oldI + 1]; j++)
                {
                    if (_colIndices[j] == nodeId) continue;
                    newColIndices.Add(_colIndices[j]);
                    newValues?.Add(_values![j]);
                }
                newRowPointers.Add(newColIndices.Count);
            }

            _nodeIdToIndexMapper = newMapper;
            _rowPointers = newRowPointers.ToArray();
            _colIndices = newColIndices.ToArray();
            _values = newValues?.ToArray();
        }

        /// <summary>
        /// Clears all edges and ego nodes from the layer.
        /// </summary>
        public void ClearLayer()
        {
            _nodeIdToIndexMapper.Clear();
            _rowPointers = [0];
            _colIndices = [];
            _values = IsValued ? [] : null;
        }

        /// <summary>
        /// Creates a filtered copy of this layer keeping only nodes present in
        /// <paramref name="nodeset"/>.
        /// </summary>
        public ILayer CreateFilteredCopy(Nodeset nodeset)
        {
            HashSet<uint> keepIds = [.. nodeset.NodeIdArray];

            var newMapper = new Dictionary<uint, int>();
            var newColIndices = new List<uint>();
            List<float>? newValues = IsValued ? [] : null;
            var newRowPointers = new List<int> { 0 };

            int newIndex = 0;
            foreach (uint egoId in _nodeIdToIndexMapper.Keys.OrderBy(id => _nodeIdToIndexMapper[id]))
            {
                if (!keepIds.Contains(egoId)) continue;
                int oldI = _nodeIdToIndexMapper[egoId];
                newMapper[egoId] = newIndex++;
                for (int j = _rowPointers[oldI]; j < _rowPointers[oldI + 1]; j++)
                {
                    if (!keepIds.Contains(_colIndices[j])) continue;
                    newColIndices.Add(_colIndices[j]);
                    newValues?.Add(_values![j]);
                }
                newRowPointers.Add(newColIndices.Count);
            }

            return new LayerOneModeCSR
            {
                Name = Name,
                Directionality = Directionality,
                EdgeValueType = EdgeValueType,
                Selfties = Selfties,
                _nodeIdToIndexMapper = newMapper,
                _rowPointers = newRowPointers.ToArray(),
                _colIndices = newColIndices.ToArray(),
                _values = newValues?.ToArray()
            };
        }
        #endregion
    }
}
