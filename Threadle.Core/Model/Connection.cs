using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public struct Connection
    {
        public uint partnerNodeId;
        public float value;

        public Connection(uint partnerNodeId, float value)
        {
            this.partnerNodeId = partnerNodeId;
            this.value = value;
        }
    }
}
