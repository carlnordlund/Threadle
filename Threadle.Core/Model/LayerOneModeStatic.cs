using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Model
{
    public class LayerOneModeStatic : ILayer, ILayerOneMode
    {
        #region Fields
        /// <summary>
        ///
        /// </summary>
        private Dictionary<uint, int> _nodeIdToIndexMapper;
        private int[] _offsets;
        private uint[] _neighborNodeIds;
        private float[]? _values;
        // For directed layers: reverse CSR storing inbound neighbors per node
        private int[]? _inOffsets;
        private uint[]? _inNeighborNodeIds;
        #endregion


        #region Constructors
        private LayerOneModeStatic(string name, EdgeDirectionality directionality, EdgeType edgeValueType, bool selfties, Dictionary<uint, int> nodeIdToIndexMapper, int[] offsets, uint[] neighborNodeIds, float[]? values, int[]? inOffsets = null, uint[]? inNeighborNodeIds = null)
        {
            Name = name;
            Directionality = directionality;
            EdgeValueType = edgeValueType;
            Selfties = selfties;
            _nodeIdToIndexMapper = nodeIdToIndexMapper;
            _offsets = offsets;
            _neighborNodeIds = neighborNodeIds;
            _values = values;
            _inOffsets = inOffsets;
            _inNeighborNodeIds = inNeighborNodeIds;
        }
        #endregion


        #region Properties
        /// <summary>
        /// The name of the layer.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Directionality of edges in the layer.
        /// </summary>
        public EdgeDirectionality Directionality { get; }

        /// <summary>
        /// Value type of edges in the layer.
        /// </summary>
        public EdgeType EdgeValueType { get; }

        /// <summary>
        /// Boolean indicating whether selfties are allowed.
        /// </summary>
        public bool Selfties { get; }

        /// <summary>
        /// Returns true if the layer is symmetric, otherwise false.
        /// </summary>
        public bool IsSymmetric => Directionality == EdgeDirectionality.Undirected;

        /// <summary>
        /// Returns true if the layer is directional, otherwise false.
        /// </summary>
        public bool IsDirectional => Directionality == EdgeDirectionality.Directed;

        /// <summary>
        /// Returns true if the layer has binary edges, otherwise false.
        /// </summary>
        public bool IsBinary => EdgeValueType == EdgeType.Binary;

        /// <summary>
        /// Returns true if the layer has valued edges, otherwise false.
        /// </summary>
        public bool IsValued => EdgeValueType == EdgeType.Valued;

        /// <summary>
        /// Returns the number of edges in the layer.
        /// </summary>
        public ulong NbrEdges => (ulong)(_neighborNodeIds.Length / (IsDirectional ? 1 : 2));

        public int NodeCount => _nodeIdToIndexMapper.Count;

        /// <summary>
        /// Returns metadata about the layer (as a dictionary of objects).
        /// </summary>
        public Dictionary<string, object> GetMetadata => new()
        {
            ["Name"] = Name,
            ["Mode"] = 1,
            ["Static"] = true,
            ["Directionality"] = Directionality.ToString(),
            ["ValueType"] = EdgeValueType.ToString(),
            ["SelftiesAllowed"] = Selfties,
            ["NbrEdges"] = NbrEdges,
            ["EstimatedMemory"] = Utilities.Misc.FormatBytes(GetEstimatedBytes())
        };


        /// <summary>
        /// Returns a string with metadata info about the layer
        /// </summary>
        public string GetLayerInfo => $" {Name} [1-mode; {EdgeValueType},{Directionality},{Selfties}; Nbr edges:{NbrEdges}]";

        public bool IsStatic => true;
        #endregion


        #region Methods (public)

        public static LayerOneModeStatic FromBinaryNodelistRows(string name, EdgeDirectionality directionality, EdgeType edgeType, bool selfties, List<(uint ego, uint[] alters)> rows)
        {
            bool isSymmetric = directionality == EdgeDirectionality.Undirected;

            // For symmetric: expand half-adjacency to full-adjacency
            var adjList = new Dictionary<uint, List<uint>>(rows.Count * 2);
            foreach (var (ego, alters) in rows)
            {
                if (!adjList.TryGetValue(ego, out var egoList))
                    adjList[ego] = egoList = new List<uint>(alters.Length);
                foreach (uint alter in alters)
                {
                    egoList.Add(alter);
                    if (isSymmetric)
                    {
                        if (!adjList.TryGetValue(alter, out var alterList))
                            adjList[alter] = alterList = [];
                        alterList.Add(ego);
                    }
                }
            }
            uint[] egoNodes = [.. adjList.Keys.Order()];
            int n = egoNodes.Length;
            var mapper = new Dictionary<uint, int>(n);
            var offsets = new int[n + 1];
            var neighborList = new List<uint>();
            for (int i = 0; i < n; i++)
            {
                mapper[egoNodes[i]] = i;
                offsets[i] = neighborList.Count;
                var neighbors = adjList[egoNodes[i]];
                neighbors.Sort();
                neighborList.AddRange(neighbors);
            }
            offsets[n] = neighborList.Count;
            uint[] finalNeighborIds = [.. neighborList];

            int[]? inOffsets = null;
            uint[]? inNeighborIds = null;
            if (!isSymmetric)
                (inOffsets, inNeighborIds) = _buildInboundFromOutbound(mapper, offsets, finalNeighborIds);

            return new LayerOneModeStatic(name, directionality, edgeType, selfties,
                mapper, offsets, finalNeighborIds, null, inOffsets, inNeighborIds);
        }

        public static LayerOneModeStatic FromValuedNodelistRows(
            string name, EdgeDirectionality directionality, EdgeType edgeType, bool selfties,
            List<(uint ego, List<(uint alter, float value)> alters)> rows)
        {
            bool isSymmetric = directionality == EdgeDirectionality.Undirected;

            var adjList = new Dictionary<uint, List<(uint alter, float value)>>(rows.Count * 2);
            foreach (var (ego, alters) in rows)
            {
                if (!adjList.TryGetValue(ego, out var egoList))
                    adjList[ego] = egoList = new List<(uint, float)>(alters.Count);
                foreach (var (alter, value) in alters)
                {
                    egoList.Add((alter, value));
                    if (isSymmetric)
                    {
                        if (!adjList.TryGetValue(alter, out var alterList))
                            adjList[alter] = alterList = [];
                        alterList.Add((ego, value));
                    }
                }
            }

            uint[] egoNodes = [.. adjList.Keys.Order()];
            int n = egoNodes.Length;
            var mapper = new Dictionary<uint, int>(n);
            var offsets = new int[n + 1];
            var neighborList = new List<uint>();
            var valueList = new List<float>();
            for (int i = 0; i < n; i++)
            {
                mapper[egoNodes[i]] = i;
                offsets[i] = neighborList.Count;
                var neighbors = adjList[egoNodes[i]];
                neighbors.Sort((a, b) => a.alter.CompareTo(b.alter));
                foreach (var (alter, value) in neighbors)
                {
                    neighborList.Add(alter);
                    valueList.Add(value);
                }
            }
            offsets[n] = neighborList.Count;
            uint[] finalNeighborIds = [.. neighborList];
            float[] finalValues = [.. valueList];

            int[]? inOffsets = null;
            uint[]? inNeighborIds = null;
            if (!isSymmetric)
                (inOffsets, inNeighborIds) = _buildInboundFromOutbound(mapper, offsets, finalNeighborIds);

            return new LayerOneModeStatic(name, directionality, edgeType, selfties,
                mapper, offsets, finalNeighborIds, finalValues, inOffsets, inNeighborIds);
        }

        public static LayerOneModeStatic FromDynamic(LayerOneMode source)
        {
            source._sortEdgesets();
            uint[] egoNodes = [.. source.Edgesets.Keys.Order()];
            int n = egoNodes.Length;
            var mapper = new Dictionary<uint, int>(n);
            var offsets = new int[n + 1];
            var neighborNodeIds = new List<uint>();
            List<float> valueList = [];

            if (source.IsValued)
                for (int i = 0; i < n; i++)
                {
                    uint egoId = egoNodes[i];
                    mapper[egoId] = i;
                    offsets[i] = neighborNodeIds.Count;
                    IEdgesetValued edgeset = (IEdgesetValued)source.Edgesets[egoId];
                    foreach (Connection c in edgeset.GetOutboundConnections)
                    {
                        neighborNodeIds.Add(c.partnerNodeId);
                        valueList.Add(c.value);
                    }
                }
            else
                for (int i = 0; i < n; i++)
                {
                    uint egoId = egoNodes[i];
                    mapper[egoId] = i;
                    offsets[i] = neighborNodeIds.Count;
                    IEdgesetBinary edgeset = (IEdgesetBinary)source.Edgesets[egoId];
                    foreach (uint partnerNodeId in edgeset.GetOutboundNodeIds)
                        neighborNodeIds.Add(partnerNodeId);
                }
            offsets[n] = neighborNodeIds.Count;
            uint[] finalNeighborNodeIds = [.. neighborNodeIds];
            int[]? inOffsets = null;
            uint[]? inNeighborNodeIds = null;
            if (source.IsDirectional)
                (inOffsets,inNeighborNodeIds) = _buildInboundFromOutbound(mapper, offsets, finalNeighborNodeIds);



            return new LayerOneModeStatic(source.Name, source.Directionality, source.EdgeValueType, source.Selfties, mapper, offsets, finalNeighborNodeIds, valueList.Count > 0 ? [.. valueList] : null, inOffsets, inNeighborNodeIds);
        }

        public float GetEdgeValue(uint node1Id, uint node2Id)
        {
            // Early exit if node is not in layer
            if (!_nodeIdToIndexMapper.TryGetValue(node1Id, out int index))
                return 0f;
            int start = _offsets[index], end = _offsets[index + 1];
            int pos = Array.BinarySearch(_neighborNodeIds, start, end - start, node2Id);
            if (pos < 0)
                return 0f;
            return _values != null ? _values[pos] : 1f;
        }

        public bool CheckEdgeExists(uint node1Id, uint node2Id)
        {
            // Early exit if node is not in layer
            if (!_nodeIdToIndexMapper.TryGetValue(node1Id, out int index))
                return false;
            int start = _offsets[index], end = _offsets[index + 1];
            int pos = Array.BinarySearch(_neighborNodeIds, start, end - start, node2Id);
            return pos >= 0;
        }


        public uint[] GetNodeAlters(uint nodeId, EdgeTraversal edgeTraversal)
        {
            // Early exit if node is not in layer
            if (!_nodeIdToIndexMapper.TryGetValue(nodeId, out int index))
                return [];

            // For undirected layers traversal direction is irrelevant — just return stored neighbors
            if (!IsDirectional || edgeTraversal == EdgeTraversal.Out)
            {
                int start = _offsets[index], end = _offsets[index + 1];
                uint[] uints = new uint[end - start];
                Array.Copy(_neighborNodeIds, start, uints, 0, end - start);
                return uints;
            }

            if (edgeTraversal== EdgeTraversal.In)
            {
                if (_inOffsets == null || _inNeighborNodeIds == null)
                    return [];
                int start = _inOffsets[index], end = _inOffsets[index + 1];
                uint[] uints = new uint[end - start];
                Array.Copy(_inNeighborNodeIds, start, uints, 0, end - start);
                return uints;
            }

            // Otherwise, edgetraversal.both: union of out- and in-neighbors (deduplicated)
            int outStart = _offsets[index], outEnd = _offsets[index + 1];
            int inStart = _inOffsets != null ? _inOffsets[index] : 0;
            int inEnd = _inOffsets != null ? _inOffsets[index+1] : 0;
            var result = new HashSet<uint>(outEnd - outStart + inEnd - inStart);
            for (int j = outStart; j < outEnd; j++)
                result.Add(_neighborNodeIds[j]);
            if (_inNeighborNodeIds != null)
                for (int j = inStart; j < inEnd; j++)
                    result.Add(_inNeighborNodeIds[j]);
            return [.. result];
        }

        /// <summary>
        /// Retrieves a collection of edges with their associated values, starting from a specified offset and limited
        /// to a maximum number of results.
        /// </summary>
        /// <param name="offset">The number of edges to skip before beginning to collect results. Must be greater than or equal to 0.</param>
        /// <param name="limit">The maximum number of edges to return. Must be greater than 0.</param>
        /// <returns>A list of dictionaries, each containing the identifiers of two connected nodes and the associated value. The
        /// list may be empty if no edges are available after applying the offset.</returns>
        public uint GetOutDegree(uint nodeId)
        {
            if (!_nodeIdToIndexMapper.TryGetValue(nodeId, out int index))
                return 0;
            return (uint)(_offsets[index + 1] - _offsets[index]);
        }

        public uint GetInDegree(uint nodeId)
        {
            if (!_nodeIdToIndexMapper.TryGetValue(nodeId, out int index))
                return 0;
            if (!IsDirectional || _inOffsets == null)
                return (uint)(_offsets[index + 1] - _offsets[index]);
            return (uint)(_inOffsets[index + 1] - _inOffsets[index]);
        }

        public List<Dictionary<string, object>> GetAllEdges(int offset = 0, int limit = 1000)
        {
            List<Dictionary<string, object>> edges = [];
            int skipped = 0;
            foreach (uint egoId in _nodeIdToIndexMapper.Keys.OrderBy(id => _nodeIdToIndexMapper[id])) // Ordered by incr egoId? Not needed actually, but nicer?
            {
                if (edges.Count >= limit)
                    break;
                int i = _nodeIdToIndexMapper[egoId];
                int start = _offsets[i], end = _offsets[i + 1];
                if (IsDirectional && skipped + end-start <= offset)
                {
                    skipped += end - start;
                    continue;
                }
                for (int j = start; j < end; j++)
                {
                    if (IsSymmetric && _neighborNodeIds[j] <= egoId)
                        continue;
                    if (edges.Count >= limit)
                        return edges;

                    if (skipped<offset)
                    {
                        skipped++;
                        continue;
                    }
                    edges.Add(new Dictionary<string, object>
                    {
                        ["node1"] = egoId,
                        ["node2"] = _neighborNodeIds[j],
                        ["value"] = _values != null ? _values[j] : 1f
                    });
                }
            }
            return edges;
        }

        public void RemoveNodeEdges(uint nodeId)
        {
            var _newMapper = new Dictionary<uint, int>(_nodeIdToIndexMapper.Count);
            var _newNeighborNodeIds = new List<uint>(_neighborNodeIds.Length);
            List<float>? _newValues = IsValued ? new List<float>(_neighborNodeIds.Length) : null;

            int _newIndex = 0;
            var _newOffsets = new List<int> { 0 };
            foreach (uint egoId in _nodeIdToIndexMapper.Keys.OrderBy(id => _nodeIdToIndexMapper[id]))
            {
                if (egoId == nodeId)
                    continue;
                int oldIndex = _nodeIdToIndexMapper[egoId];
                _newMapper[egoId] = _newIndex++;
                for (int j = _offsets[oldIndex]; j < _offsets[oldIndex + 1]; j++)
                {
                    if (_neighborNodeIds[j] == nodeId)
                        continue;
                    _newNeighborNodeIds.Add(_neighborNodeIds[j]);
                    _newValues?.Add(_values![j]);
                }
                _newOffsets.Add(_newNeighborNodeIds.Count);
            }
            _nodeIdToIndexMapper = _newMapper;
            _offsets = _newOffsets.ToArray();
            _neighborNodeIds = _newNeighborNodeIds.ToArray();
            _values = _newValues?.ToArray();
            if (IsDirectional)
                (_inOffsets, _inNeighborNodeIds) = _buildInboundFromOutbound(_nodeIdToIndexMapper, _offsets, _neighborNodeIds);
        }

        public void ClearLayer()
        {
            _nodeIdToIndexMapper.Clear();
            _offsets = [];
            _neighborNodeIds = [];
            _values = IsValued ? [] : null;
            _inOffsets = IsDirectional ? [] : null;
            _inNeighborNodeIds = IsDirectional ? [] : null;
        }

        public ILayer CreateFilteredCopy(Nodeset nodeset)
        {
            var _newMapper = new Dictionary<uint, int>(_nodeIdToIndexMapper.Count);
            var _newNeighborNodeIds = new List<uint>(_neighborNodeIds.Length);
            List<float>? _newValues = IsValued ? new List<float>(_neighborNodeIds.Length) : null;

            int _newIndex = 0;
            var _newOffsets = new List<int> { 0 };
            foreach (uint egoId in _nodeIdToIndexMapper.Keys.OrderBy(id => _nodeIdToIndexMapper[id]))
            {
                if (!nodeset.Contains(egoId))
                    continue;
                int oldIndex = _nodeIdToIndexMapper[egoId];
                _newMapper[egoId] = _newIndex++;
                for (int j = _offsets[oldIndex]; j < _offsets[oldIndex + 1]; j++)
                {
                    //if (_neighborNodeIds[j] == nodeId)
                    if (!nodeset.Contains(_neighborNodeIds[j]))
                        continue;
                    _newNeighborNodeIds.Add(_neighborNodeIds[j]);
                    _newValues?.Add(_values![j]);
                }
                _newOffsets.Add(_newNeighborNodeIds.Count);
            }
            uint[] filteredNeighborNodeIds = [.. _newNeighborNodeIds];
            int[] filteredOffsets = [.. _newOffsets];
            int[]? filteredInOffsets = null;
            uint[]? filteredInNeighborNodeIds = null;
            if (IsDirectional)
                (filteredInOffsets, filteredInNeighborNodeIds) = _buildInboundFromOutbound(_newMapper, filteredOffsets, filteredNeighborNodeIds);


            return new LayerOneModeStatic(this.Name + "_filtered", this.Directionality, this.EdgeValueType, this.Selfties, _newMapper, filteredOffsets, filteredNeighborNodeIds, _newValues?.ToArray(), filteredInOffsets, filteredInNeighborNodeIds);
        }


        public List<string> GetNFirstEdges(int n = 10)
        {
            List<string> edges = new(n);
            string arrow = IsSymmetric ? "<->" : "->";
            foreach (uint egoId in _nodeIdToIndexMapper.Keys)
            {
                if (edges.Count >= n)
                    break;
                int i=_nodeIdToIndexMapper[egoId];
                int start = _offsets[i], end = _offsets[i + 1];
                for (int j = start; j < end && edges.Count < n; j++)
                {
                    uint alterId = _neighborNodeIds[j];
                    if (IsSymmetric && alterId <= egoId)
                        continue;
                    edges.Add(IsBinary ? $"{egoId} {arrow} {alterId}" : $"{egoId} {arrow} {alterId} ({_values![j]})");
                }
            }
            return edges;
        }

        public long GetEstimatedBytes()
        {
            int N = _nodeIdToIndexMapper.Count;
            int M = _neighborNodeIds.Length;
            // Memory for the node id to index lookup: _nodeIdToIndexMapper
            long bytes = (long)N * 16 + (long)(N / 0.72 + 1) * 4;
            // Memory for the _offsets[] array (+1 for the final cell)
            bytes += (long)(N + 1) * 4;
            // Memory for all partnerNodeIds: _ neighborNodeIds
            bytes += (long)M * 4;
            // Memory for valued array _values[] (only for valued layers)
            if (_values != null)
                bytes += (long)M * 4;
            return bytes;
        }

        public IEnumerable<(uint egoId, ReadOnlyMemory<uint> alters, ReadOnlyMemory<float> values)> GetAllEgoData()
        {
            // Iterate through all node Ids in this layer
            foreach (var (egoId, index) in _nodeIdToIndexMapper)
            {
                int start = _offsets[index], end = _offsets[index + 1];
                if (IsSymmetric)
                {
                    var filteredAlters = new List<uint>(end - start);
                    List<float>? filteredValues = _values != null ? new List<float>(end - start) : null;
                    for (int j = start; j < end; j++)
                    {
                        if (_neighborNodeIds[j] > egoId)
                        {
                            filteredAlters.Add(_neighborNodeIds[j]);
                            filteredValues?.Add(_values![j]);
                        }
                    }
                    if (filteredAlters.Count > 0)
                        yield return (egoId, filteredAlters.ToArray(), filteredValues?.ToArray());
                }
                else
                {
                    uint[] alters = new uint[end - start];
                    Array.Copy(_neighborNodeIds, start, alters, 0, end - start);
                    float[]? values = _values != null ? _values[start..end] : null;
                    yield return (egoId, alters, values);
                }
            }
        }
        #endregion


        #region Methods (internal, private)
        /// <summary>
        /// Builds the inbound CSR from the current outbound CSR. For each out-edge X→Y,
        /// Y gains X as an inbound neighbor. Only called for directed layers.
        /// </summary>
        private static (int[] inOffsets, uint[] inNeighborIds) _buildInboundFromOutbound(
            Dictionary<uint, int> mapper, int[] outOffsets, uint[] outNeighborNodeIds)
        {
            int n = mapper.Count;

            uint[] egoIds = new uint[n];
            foreach (var (nodeId, index) in mapper)
                egoIds[index] = nodeId;

            var inNeighborsPerNode = new List<uint>[n];
            for (int i = 0; i < n; i++)
                inNeighborsPerNode[i] = [];

            for (int i = 0; i < n; i++)
            {
                int start = outOffsets[i], end = outOffsets[i + 1];
                for (int j = start; j < end; j++)
                {
                    uint neighborId = outNeighborNodeIds[j];
                    if (mapper.TryGetValue(neighborId, out int neighborIdx))
                        inNeighborsPerNode[neighborIdx].Add(egoIds[i]);
                }
            }

            var inOffsets = new int[n + 1];
            var inNeighborList = new List<uint>();
            for (int i = 0; i < n; i++)
            {
                inOffsets[i] = inNeighborList.Count;
                inNeighborsPerNode[i].Sort();
                inNeighborList.AddRange(inNeighborsPerNode[i]);
            }
            inOffsets[n] = inNeighborList.Count;
            return ([.. inOffsets], [.. inNeighborList]);
        }
        #endregion
    }
}
