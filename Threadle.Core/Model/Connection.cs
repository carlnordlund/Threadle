using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public struct Connection(uint partnerNodeId, float value)
    {
        public uint partnerNodeId = partnerNodeId;
        public float value = value;
    }
}
