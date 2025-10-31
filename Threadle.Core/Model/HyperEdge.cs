using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes an affiliation, e.g. like a specific school class, a specific workplace or similar
    /// which is then an HyperEdge: a collection of Nodes that are connected.
    /// Each such affiliation should have a label (e.g. arbplatsId), but that is stored
    /// in LayerTwomode.AllHyperEdges dictionary
    /// </summary>
    public class HyperEdge
    {
        /// <summary>
        /// Decided to store this as a List, not a HashSet: saves memory, and not really needed to have this info actually!
        /// </summary>
        public List<uint> nodeIds;

        public int NbrNodes => nodeIds.Count;

        public HyperEdge()
        {
            nodeIds = [];
        }

        public HyperEdge(uint[] nodeIds)
        {
            this.nodeIds = [.. nodeIds];
        }

        internal void Clear()
        {
            nodeIds.Clear();
        }
    }
}
