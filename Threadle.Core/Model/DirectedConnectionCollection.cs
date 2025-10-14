using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Model
{
    public class DirectedConnectionCollection : IConnectionCollection
    {
        private List<Connection> _outbound = new();
        private List<Connection> _inbound = new();

        public List<Connection> GetOutboundConnections() => _outbound;
        public List<Connection> GetInboundConnections() => _inbound;

        public int GetNbrOutboundConnections() => _outbound.Count;
        public int GetNbrInboundConnections() => _inbound.Count;

        public void AddInboundConnection(Connection connection)
        {
            _inbound.Add(connection);
        }

        public void AddOutboundConnection(Connection connection)
        {
            _outbound.Add(connection);
        }
    }
}
