using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes a collection of Hyperedge references that belong to a
    /// specific node. I.e. each node has its own HyperedgeCollection, which
    /// thus contains all Hyperedge objects that this Node is part of
    /// </summary>
    public class HyperedgeCollection
    {
        #region Fields
        /// <summary>
        /// The set of Hyperedge objects in this collection
        /// </summary>
        private HashSet<Hyperedge> _hyperedges = [];
        #endregion


        #region Constructors
        /// <summary>
        /// If a node does not have a HyperedgeCollection when a Hyperedge is added to it,
        /// a HyperedgeCollection must be created that contains that particular Hyperedge.
        /// This constructor does both of these things.
        /// </summary>
        /// <param name="hyperedge">The (first) Hyperedge to add to this collection.</param>
        public HyperedgeCollection(Hyperedge hyperedge)
        {
            AddHyperEdge(hyperedge);
        }
        #endregion


        #region Properties
        /// <summary>
        /// Returns the set of Hyperedge objects
        /// </summary>
        public HashSet<Hyperedge> HyperEdges => _hyperedges;
        #endregion


        #region Methods
        /// <summary>
        /// Adds a Hyperedge object to this collection
        /// </summary>
        /// <param name="hyperedge">The Hyperedge object to add.</param>
        internal void AddHyperEdge(Hyperedge hyperedge)
        {
            _hyperedges.Add(hyperedge);
        }
        #endregion
    }
}
