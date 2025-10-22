using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public class EdgesetBinaryDirectional : IEdgesetBinary, IEdgesetDirectional
    {
        private List<uint> _outbound = new();
        private List<uint> _inbound = new();

        public List<uint> GetOutboundConnections() => _outbound;
        public List<uint> GetInboundConnections() => _inbound;
        public List<uint> GetAllNodeIds() => _outbound.Concat(_inbound).ToList();

        public uint NbrOutboundEdges { get => (uint)_outbound.Count; }
        public uint NbrInboundEdges { get => (uint)_inbound.Count; }
        public uint NbrEdges { get => (uint)_outbound.Count; }

        public List<uint> GetOutboundNodeIds() => _outbound;
        public List<uint> GetInboundNodeIds() => _inbound;


        public OperationResult AddInboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _inbound.Contains(partnerNodeId))
                return OperationResult.Fail("EdgeExists","Edge already exists (blocked)");
            _inbound.Add(partnerNodeId);
            return OperationResult.Ok();
        }

        public OperationResult AddOutboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _outbound.Contains(partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)"); ;
            _outbound.Add(partnerNodeId);
            return OperationResult.Ok();
        }

        public float GetOutboundPartnerEdgeValue(uint partnerNodeId)
        {
            return (_outbound.Contains(partnerNodeId)) ? 1 : 0;
        }

        public bool CheckOutboundPartnerEdgeExists(uint partnerNodeId)
        {
            return _outbound.Contains(partnerNodeId);
        }

        public string GetNodelistAlterString(uint egoNodeId)
        {
            string ret = "";
            foreach (uint alterNodeId in _outbound)
                ret += "\t" + alterNodeId;
            return ret;
        }

        public uint[] GetAlterIds(EdgeTraversal edgeTraversal)
        {
            switch (edgeTraversal)
            {
                case EdgeTraversal.Out:
                    return _outbound.Count > 0 ? _outbound.ToArray() : Array.Empty<uint>();
                case EdgeTraversal.In:
                    return _inbound.Count > 0 ? _inbound.ToArray() : Array.Empty<uint>();
                case EdgeTraversal.Both:
                    if (_outbound.Count == 0) return _inbound.Count > 0 ? _inbound.ToArray() : Array.Empty<uint>();
                    if (_inbound.Count == 0) return _outbound.ToArray();

                    var union = new HashSet<uint>(_outbound.Count + _inbound.Count);
                    foreach (var id in _outbound) union.Add(id);
                    foreach (var id in _inbound) union.Add(id);
                    return union.Count > 0 ? union.ToArray() : Array.Empty<uint>();
                default:
                    return Array.Empty<uint>();
            }
        }

        public void ClearEdges()
        {
            _outbound.Clear();
            _inbound.Clear();
        }
    }
}
