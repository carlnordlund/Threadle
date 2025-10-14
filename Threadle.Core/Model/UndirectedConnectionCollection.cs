using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Model
{
    public class UndirectedConnectionCollection : IConnectionCollection
    {
        private List<Connection> _connections = new();


        public List<Connection> GetOutboundConnections() => _connections;
        public List<Connection> GetInboundConnections() => _connections;
        public int GetNbrOutboundConnections() => _connections.Count;
        public int GetNbrInboundConnections() => _connections.Count;

        public void AddInboundConnection(Connection connection)
        {
            _connections.Add(connection);
        }

        public void AddOutboundConnection(Connection connection)
        {
            _connections.Add(connection);
        }


    }
}
