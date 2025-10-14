using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core
{
    public interface IEdgeCollection
    {
        IEnumerable<Edge> GetOutboundEdges();
        IEnumerable<Edge> GetInboundEdges();
        void AddEdge(Edge edge, Node context);
    }
}
