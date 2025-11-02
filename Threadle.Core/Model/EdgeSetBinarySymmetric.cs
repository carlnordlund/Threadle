using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public class EdgesetBinarySymmetric : IEdgesetBinary, IEdgesetSymmetric
    {
        private readonly List<uint> _connections = [];

        public List<uint> GetOutboundConnections { get => _connections; }
        public List<uint> GetInboundConnections { get => _connections; }

        public uint NbrOutboundEdges { get => (uint)_connections.Count; }
        public uint NbrInboundEdges { get => (uint)_connections.Count; }
        public uint NbrEdges { get => (uint)_connections.Count; }


        public List<uint> GetOutboundNodeIds { get => _connections; }
        public List<uint> GetInboundNodeIds { get => _connections; }
        public List<uint> GetAllNodeIds { get => _connections; }


        public OperationResult AddInboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _connections.Contains(partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)"); ;
            _connections.Add(partnerNodeId);
            return OperationResult.Ok();
        }

        public OperationResult AddOutboundEdge(uint partnerNodeId, float value = 1)
        {
            if (UserSettings.BlockMultiedges && _connections.Contains(partnerNodeId))
                return OperationResult.Fail("EdgeExists", "Edge already exists (blocked)"); ; ;
            _connections.Add(partnerNodeId);                
            return OperationResult.Ok();
        }

        public OperationResult RemoveInboundEdge(uint partnerNodeId)
        {
            if (_connections.Remove(partnerNodeId))
                return OperationResult.Ok();
            return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }

        public OperationResult RemoveOutboundEdge(uint partnerNodeId)
        {
            if (_connections.Remove(partnerNodeId))
                return OperationResult.Ok();
            return OperationResult.Fail("EdgeNotFound", "Edge not found.");
        }
        public void RemoveNodeEdges(uint nodeId)
        {
            _connections.Remove(nodeId);
        }


        public float GetOutboundPartnerEdgeValue(uint partnerNodeId)
        {
            return (_connections.Contains(partnerNodeId)) ? 1 : 0;
        }

        public bool CheckOutboundPartnerEdgeExists(uint partnerNodeId)
        {
            return _connections.Contains(partnerNodeId);
        }

        public string GetNodelistAlterString(uint egoNodeId)
        {
            string ret = "";
            
            foreach (uint alterNodeId in _connections)
                if (alterNodeId > egoNodeId)
                    ret += "\t" + alterNodeId;

            return ret;
        }

        public uint[] GetAlterIds(EdgeTraversal edgeTraversal)
        {
            return [.. _connections];
        }

        public void ClearEdges()
        {
            _connections.Clear();
        }
    }
}
