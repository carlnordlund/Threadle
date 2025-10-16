using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public interface IEdgeset
    {
        uint NbrOutboundEdges { get; }
        uint NbrInboundEdges { get; }
        uint NbrEdges { get; }
        
        OperationResult AddInboundEdge(uint partnerNodeId, float value);
        OperationResult AddOutboundEdge(uint partnerNodeId, float value);

        List<uint> GetOutboundNodeIds();
        List<uint> GetInboundNodeIds();
        List<uint> GetAllNodeIds();

        float GetOutboundPartnerEdgeValue(uint partnerNodeId);
        bool CheckOutboundPartnerEdgeExists(uint partnerNodeId);

        /// <summary>
        /// This method produces a nodelist2-style string with alters, intended for save files.
        /// Note that for symmetric edges, I only want to store these once, so therefore I have
        /// to pass along the nodeId to differentiate between two nodes (only store ties when
        /// nodeid_from less-than nodeid_to.
        /// This method is thus not useful for getting Alter ids!
        /// </summary>
        /// <param name="nodeId">Reference node id</param>
        /// <returns>string</returns>
        string GetNodelistAlterString(uint nodeId);

        uint[] GetAlterIds(EdgeTraversal edgeTraversal);
        void ClearEdges();

    }
}
