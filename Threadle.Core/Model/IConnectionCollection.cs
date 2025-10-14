using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Model
{
    public interface IConnectionCollection
    {
        List<Connection> GetOutboundConnections();
        List<Connection> GetInboundConnections();
        int GetNbrOutboundConnections();
        int GetNbrInboundConnections();
        void AddInboundConnection(Connection connection);
        void AddOutboundConnection(Connection connection);
    }
}
