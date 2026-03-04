using Threadle.Core.Model.Enums;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Read-optimized Compressed Sparse Row representation of a 1-mode layer.
    /// Converted from a <see cref="LayerOneMode"/> via <see cref="FromDynamic"/>.
    /// Outbound (or symmetric) edges are stored per ego node in sorted order.
    /// </summary>
    public class LayerOneModeCSR : ILayer
    {
        #region Fields
        /// <summary>Sorted ego node IDs.</summary>
        private uint[] _nodeIds = [];

        /// <summary>
        /// Row pointers into <see cref="_colIndices"/>. Node i's neighbors span
        /// [_rowPointers[i], _rowPointers[i+1]).
        /// </summary>
        private int[] _rowPointers = [0];

        /// <summary>Flat array of alter node IDs, grouped by ego.</summary>
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

            var rowPointers = new int[nNodes + 1];
            var colIndicesList = new List<uint>();
            List<float>? valuesList = layer.IsValued ? [] : null;

            for (int i = 0; i < nNodes; i++)
            {
                uint egoId = sortedNodeIds[i];
                IEdgeset edgeset = layer.Edgesets[egoId];
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
                _nodeIds = sortedNodeIds,
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

            for (int i = 0; i < _nodeIds.Length && edges.Count < n; i++)
            {
                uint egoId = _nodeIds[i];
                int start = _rowPointers[i];
                int end = _rowPointers[i + 1];

                for (int j = start; j < end && edges.Count < n; j++)
                {
                    uint alterId = _colIndices[j];
                    // For symmetric layers, display each edge once (same as dynamic FormatEdges)
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
            HashSet<uint> ids = [.. _nodeIds];
            foreach (uint alterId in _colIndices)
                ids.Add(alterId);
            return ids;
        }

        public float GetEdgeValue(uint node1Id, uint node2Id)
        {
            int row = Array.BinarySearch(_nodeIds, node1Id);
            if (row < 0) return 0f;

            int start = _rowPointers[row];
            int end = _rowPointers[row + 1];
            int col = Array.BinarySearch(_colIndices, start, end - start, node2Id);
            if (col < 0) return 0f;

            return IsValued ? _values![col] : 1f;
        }

        public bool CheckEdgeExists(uint node1Id, uint node2Id)
        {
            int row = Array.BinarySearch(_nodeIds, node1Id);
            if (row < 0) return false;

            int start = _rowPointers[row];
            int end = _rowPointers[row + 1];
            return Array.BinarySearch(_colIndices, start, end - start, node2Id) >= 0;
        }

        public uint[] GetNodeAlters(uint nodeId, EdgeTraversal edgeTraversal)
        {
            int row = Array.BinarySearch(_nodeIds, nodeId);
            if (row < 0) return [];

            int start = _rowPointers[row];
            int end = _rowPointers[row + 1];
            return _colIndices[start..end];
        }

        /// <summary>Not supported on a read-only CSR layer.</summary>
        public void RemoveNodeEdges(uint nodeId) =>
            throw new NotSupportedException("CSR layers are read-only.");

        /// <summary>Not supported on a read-only CSR layer.</summary>
        public void ClearLayer() =>
            throw new NotSupportedException("CSR layers are read-only.");

        /// <summary>Not supported on a read-only CSR layer.</summary>
        public ILayer CreateFilteredCopy(Nodeset nodeset) =>
            throw new NotSupportedException("CSR layers are read-only.");
        #endregion
    }
}
