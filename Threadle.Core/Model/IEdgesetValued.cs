using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Interface for the two edgeset classes that contain valued edges.
    /// Implements two new properties, for getting lists of in- and outbound
    /// Connection structs.
    /// </summary>
    public interface IEdgesetValued : IEdgeset
    {
        #region Properties
        /// <summary>
        /// Returns a list of outbound Connection structs, i.e. edges that hold a partner node id and a value
        /// </summary>
        IReadOnlyList<Connection> GetOutboundConnections { get; }

        /// <summary>
        /// Returns a list of inbound Connection structs, i.e. edges that hold a partner node id and a value
        /// </summary>
        IReadOnlyList<Connection> GetInboundConnections { get; }

        /// <summary>
        /// Gets a list of all partner Connection structs. For symmetric edgesets, edges should only be stored once
        /// so I pass along egoNodeId to make sure I only get Connection structs for partner ids that are larger than ego node id
        /// </summary>
        /// <param name="egoNodeId">The node id of ego</param>
        /// <returns>A List of Connection structs.</returns>
        IReadOnlyList<Connection> GetNodelistAlterConnections(uint egoNodeId);
        #endregion
    }
}
