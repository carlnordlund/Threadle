using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public class EdgesetValuedDirectional : IEdgesetValued, IEdgesetDirectional
    {
        private readonly List<Connection> _outbound = [];
        private readonly List<Connection> _inbound = [];

        public List<Connection> GetOutboundConnections => _outbound;
        public List<Connection> GetInboundConnections => _inbound;

        public uint NbrOutboundEdges { get => (uint)_outbound.Count; }
        public uint NbrInboundEdges { get => (uint)_inbound.Count; }
        public uint NbrEdges { get => (uint)_outbound.Count; }

        public List<uint> GetOutboundNodeIds() => [.. _outbound.Select(s => s.partnerNodeId)];
        public List<uint> GetInboundNodeIds() => [.. _inbound.Select(s => s.partnerNodeId)];
        public List<uint> GetAllNodeIds() => [.. GetOutboundNodeIds().Concat(GetInboundNodeIds())];


        public OperationResult AddInboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _inbound.Any(s => s.partnerNodeId == partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)");
            _inbound.Add(new Connection(partnerNodeId, value));
            return OperationResult.Ok();
        }

        public OperationResult AddOutboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _outbound.Any(s => s.partnerNodeId == partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)");
            _outbound.Add(new Connection(partnerNodeId, value));
            return OperationResult.Ok();
        }

        public OperationResult RemoveInboundEdge(uint partnerNodeId)
        {
            int index = _inbound.FindIndex(c => c.partnerNodeId == partnerNodeId);
            if (index >= 0)
            {
                _inbound.RemoveAt(index);
                return OperationResult.Ok();
            }
            return OperationResult.Fail("EdgenotFound", "Edge not found.");
        }

        public OperationResult RemoveOutboundEdge(uint partnerNodeId)
        {
            int index = _outbound.FindIndex(c => c.partnerNodeId == partnerNodeId);
            if (index >= 0)
            {
                _outbound.RemoveAt(index);
                return OperationResult.Ok();
            }
            return OperationResult.Fail("EdgenotFound", "Edge not found.");
        }

        public void RemoveNodeEdges(uint nodeId)
        {
            _outbound.RemoveAll(c => c.partnerNodeId == nodeId);
            _inbound.RemoveAll(c => c.partnerNodeId == nodeId);
        }


        public float GetOutboundPartnerEdgeValue(uint partnerNodeId)
        {
            foreach (var connection in _outbound)
                if (connection.partnerNodeId == partnerNodeId)
                    return connection.value;
            return 0;
        }

        public bool CheckOutboundPartnerEdgeExists(uint partnerNodeId)
        {
            foreach (var connection in _outbound)
                if (connection.partnerNodeId == partnerNodeId)
                    return true;
            return false;
        }

        public string GetNodelistAlterString(uint egoNodeId)
        {
            string ret = "";
            foreach (Connection connection in _outbound)
                ret += $"\t{connection.partnerNodeId};{connection.value}";
            return ret;
        }

        public uint[] GetAlterIds(EdgeTraversal edgeTraversal)
        {
            switch (edgeTraversal)
            {
                case EdgeTraversal.Out:
                    if (_outbound.Count == 0) return [];
                    var outboundIds = new uint[_outbound.Count];
                    for (int i = 0; i < _outbound.Count; i++)
                        outboundIds[i] = _outbound[i].partnerNodeId;
                    return outboundIds;
                case EdgeTraversal.In:
                    if (_inbound.Count == 0) return [];
                    var inboundIds = new uint[_inbound.Count];
                    for (int i = 0; i < _inbound.Count; i++)
                        inboundIds[i] = _inbound[i].partnerNodeId;
                    return inboundIds;
                case EdgeTraversal.Both:
                    if (_outbound.Count == 0) return GetAlterIds(EdgeTraversal.In);
                    if (_inbound.Count == 0) return GetAlterIds(EdgeTraversal.Out);

                    var union = new HashSet<uint>(_outbound.Count + _inbound.Count);
                    for (int i = 0; i < _outbound.Count; i++)
                        union.Add(_outbound[i].partnerNodeId);
                    for (int i=0; i< _inbound.Count; i++)
                        union.Add(_inbound[i].partnerNodeId);
                    return union.Count>0 ? [.. union] : [];

                default:
                    return [];
            }
        }

        public void ClearEdges()
        {
            _outbound.Clear();
            _inbound.Clear();
        }


    }
}
