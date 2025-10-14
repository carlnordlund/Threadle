using Threadle.Core.Model.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public interface ILayer
    {
        string Name { get; set; }
        HashSet<uint> GetMentionedNodeIds();
        float GetEdgeValue(uint node1, uint node2);
        bool CheckEdgeExists(uint node1, uint node2);
        uint[] GetAlterIds(uint nodeId, EdgeTraversal edgeTraversal);

        void ClearLayer();

    }
}
