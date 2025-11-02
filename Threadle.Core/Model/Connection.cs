using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Represents a valued tie, used by the valued edgesets where both a partnerNodeId and
    /// a value of the tie must be stored (i.e. where the value could be different than a binary one)
    /// </summary>
    /// <param name="partnerNodeId">The id of the partner node.</param>
    /// <param name="value">The value of the tie (float).</param>
    public struct Connection(uint partnerNodeId, float value)
    {
        public uint partnerNodeId = partnerNodeId;
        public float value = value;
    }
}
