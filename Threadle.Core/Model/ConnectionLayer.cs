using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Model
{
    public struct ConnectionLayer
    {
        public byte LayerId;
        public IConnectionCollection Connections;

        public ConnectionLayer(byte layerId,  IConnectionCollection connections)
        {
            LayerId = layerId;
            Connections = connections;
        }
    }
}
