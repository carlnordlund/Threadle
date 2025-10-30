using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public class EdgesetValuedSymmetric : IEdgesetValued, IEdgesetSymmetric
    {
        private List<Connection> _connections = new();

        public List<Connection> GetOutboundConnections => _connections;
        public List<Connection> GetInboundConnections => _connections;

        public uint NbrOutboundEdges { get => (uint)_connections.Count; }
        public uint NbrInboundEdges { get => (uint)_connections.Count; }
        public uint NbrEdges { get => (uint)_connections.Count; }

        public List<uint> GetOutboundNodeIds() => _connections.Select(s => s.partnerNodeId).ToList();
        public List<uint> GetInboundNodeIds() => _connections.Select(s => s.partnerNodeId).ToList();
        public List<uint> GetAllNodeIds() => _connections.Select(s => s.partnerNodeId).ToList();


        public OperationResult AddInboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _connections.Any(s => s.partnerNodeId == partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)"); ;
            _connections.Add(new Connection(partnerNodeId, value));
            return OperationResult.Ok();
        }

        public OperationResult AddOutboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _connections.Any(s => s.partnerNodeId == partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)"); ;
            _connections.Add(new Connection(partnerNodeId, value));
            return OperationResult.Ok();
        }

        public OperationResult RemoveInboundEdge(uint partnerNodeId)
        {
            int index = _connections.FindIndex(c => c.partnerNodeId == partnerNodeId);
            if (index >= 0)
            {
                _connections.RemoveAt(index);
                return OperationResult.Ok();
            }
            return OperationResult.Fail("EdgenotFound", "Edge not found.");
        }

        public OperationResult RemoveOutboundEdge(uint partnerNodeId)
        {
            int index = _connections.FindIndex(c => c.partnerNodeId == partnerNodeId);
            if (index >= 0)
            {
                _connections.RemoveAt(index);
                return OperationResult.Ok();
            }
            return OperationResult.Fail("EdgenotFound", "Edge not found.");
        }

        public void RemoveNodeEdges(uint nodeId)
        {
            _connections.RemoveAll(c => c.partnerNodeId == nodeId);
        }


        public float GetOutboundPartnerEdgeValue(uint partnerNodeId)
        {
            foreach (var connection in _connections)
                if (connection.partnerNodeId == partnerNodeId)
                    return connection.value;
            return 0;
        }
        public bool CheckOutboundPartnerEdgeExists(uint partnerNodeId)
        {
            foreach (var connection in _connections)
                if (connection.partnerNodeId == partnerNodeId)
                    return true;
            return false;
        }

        public string GetNodelistAlterString(uint egoNodeId)
        {
            string ret = "";
            foreach (Connection connection in _connections)
                if (connection.partnerNodeId > egoNodeId)
                    ret += $"\t{connection.partnerNodeId};{connection.value}";
            return ret;
        }

        public uint[] GetAlterIds(EdgeTraversal edgeTraversal)
        {
            if (_connections.Count == 0)
                return Array.Empty<uint>();
            uint[] ids = new uint[_connections.Count];
            for (int i = 0; i < _connections.Count; i++)
                ids[i] = _connections[i].partnerNodeId;
            return ids;
        }

        public void ClearEdges()
        {
            _connections.Clear();
        }
    }
}
