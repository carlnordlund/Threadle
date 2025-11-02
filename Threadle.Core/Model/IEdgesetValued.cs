using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Interface for the two edgeset classes that contain valued edges.
    /// </summary>
    public interface IEdgesetValued : IEdgeset
    {
        #region Properties
        /// <summary>
        /// Returns a list of outbound Connection structs, i.e. edges that hold a partner node id and a value
        /// </summary>
        List<Connection> GetOutboundConnections { get; }

        /// <summary>
        /// Returns a list of inbound Connection structs, i.e. edges that hold a partner node id and a value
        /// </summary>
        List<Connection> GetInboundConnections { get; }
        #endregion
    }
}
