using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public interface IEdgesetValued : IEdgeset
    {
        List<Connection> GetOutboundConnections { get; }
        List<Connection> GetInboundConnections { get; }
    }
}
