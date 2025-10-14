using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes a collection of HyperEdge references that belong to a
    /// specific Node. I.e. each Node has its own HyperEdgeCollection, which
    /// thus contains all HyperEdge objects that this Node is part of
    /// </summary>
    public class HyperEdgeCollection
    {
        /// <summary>
        /// Here I went from a HashSet of HyperEdge objects to a List instead. I don't see an advantage with using Hashset here
        /// 
        /// </summary>
        public HashSet<HyperEdge> HyperEdges { get; } = [];

        /// <summary>
        /// This constructor creates an empty HyperEdgeCollection
        /// Likely useful for when loading a 2-mode dataset from file, where many
        /// HyperEdge objects are created at once
        /// </summary>
        public HyperEdgeCollection()
        {
        }

        /// <summary>
        /// If this Node doesn't have a HyperEdgeCollection when trying to add
        /// reference to a HyperEdge, the collection must be created and then the
        /// hyperedge added to this. This constructor does both.
        /// </summary>
        /// <param name="hyperEdge"></param>
        public HyperEdgeCollection(HyperEdge hyperEdge)
        {
            HyperEdges.Add(hyperEdge);
        }

        internal void AddHyperEdge(HyperEdge hyperEdge)
        {
            HyperEdges.Add(hyperEdge);
        }
    }
}
