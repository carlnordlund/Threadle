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
        #endregion


        #region Constructors
        private LayerOneModeStatic(string name, EdgeDirectionality directionality, EdgeType edgeValueType, bool selfties, Dictionary<uint, int> nodeIdToIndexMapper, int[] offsets, uint[] neighborNodeIds, float[]? values)
        {
            Name = name;
            Directionality = directionality;
            EdgeValueType = edgeValueType;
            Selfties = selfties;
            _nodeIdToIndexMapper = nodeIdToIndexMapper;
            _offsets = offsets;
            _neighborNodeIds = neighborNodeIds;
            _values = values;
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
        public uint NbrEdges => (uint)(_neighborNodeIds.Length / (IsDirectional ? 1 : 2));

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
            ["NbrEdges"] = NbrEdges
        };


        /// <summary>
        /// Returns a string with metadata info about the layer
        /// </summary>
        public string GetLayerInfo => $" {Name} [1-mode: {EdgeValueType},{Directionality},{Selfties}); Nbr edges:{NbrEdges}]";

        public bool IsStatic => true;
        #endregion


        #region Methods (public)
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
            return new LayerOneModeStatic(source.Name, source.Directionality, source.EdgeValueType, source.Selfties, mapper, offsets, [.. neighborNodeIds], valueList.Count > 0 ? [.. valueList] : null);
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
            int start = _offsets[index], end = _offsets[index + 1];
            uint[] uints = new uint[end-start];
            Array.Copy(_neighborNodeIds, start, uints, 0, end - start);
            return uints;
        }

        /// <summary>
        /// Retrieves a collection of edges with their associated values, starting from a specified offset and limited
        /// to a maximum number of results.
        /// </summary>
        /// <param name="offset">The number of edges to skip before beginning to collect results. Must be greater than or equal to 0.</param>
        /// <param name="limit">The maximum number of edges to return. Must be greater than 0.</param>
        /// <returns>A list of dictionaries, each containing the identifiers of two connected nodes and the associated value. The
        /// list may be empty if no edges are available after applying the offset.</returns>
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
        }

        public void ClearLayer()
        {
            _nodeIdToIndexMapper.Clear();
            _offsets = [];
            _neighborNodeIds = [];
            _values = IsValued ? [] : null;
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

            return new LayerOneModeStatic(this.Name + "_filtered", this.Directionality, this.EdgeValueType, this.Selfties, _newMapper, [.. _newOffsets], [.. _newNeighborNodeIds], _newValues?.ToArray());
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
        #endregion
    }
}
