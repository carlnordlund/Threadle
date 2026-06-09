using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public interface ILayerTwoMode : ILayer
    {
        #region Properties
        uint NbrHyperedges { get; }

        /// <summary>
        /// Returns the number of node-to-hyperedge affiliations.
        /// Can either be implemented exactly, or by sampling.
        /// </summary>
        long NbrAffiliations { get; }
        #endregion


        #region Methods
        /// <summary>
        /// Returns the node ids affiliated to the specified hyperedge, sorted by ascending node id.
        /// Returns an empty array if no hyperedge with that name exist, or if the hyperedge has no node ids
        /// affiliated to it.
        /// </summary>
        uint[] GetHyperedgeNodeIds(string hyperName);


        /// <summary>
        /// Returns the names of all hyperedges that the specified node is affiliated to.
        /// Returns an empty array if the node has no affiliations in this layer.
        /// </summary>
        string[] GetNodeHyperedgeNames(uint nodeId);


        /// <summary>
        /// Returns a paginated slice of all hyperedge names in the layer.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        string[] GetAllHyperedgeNames(int offset, int limit);

        /// <summary>
        /// Returns true if the layer contains a hyperedge with a specific name
        /// </summary>
        bool ContainsHyperedge(string hypername);

        /// <summary>
        /// Iterates all hyperedges, yielding the name and affiliated node ids for each.
        /// </summary>
        IEnumerable<(string hypername, uint[] nodeIds)> GetAllHyperedgeData();

        /// <summary>
        /// Picks a random projected alter for nodeId without materialising the full alter set.
        /// Single-hyperedge nodes: O(1) rejection sampling, zero allocation.
        /// Multi-hyperedge nodes: two-step proportional pick weighted by hyperedge size, O(k).
        /// Returns null if the node has no alters.
        /// </summary>
        uint? PickRandomAlterO1(uint nodeId);

        /// <summary>
        /// Streams this node's projected alters into a running reservoir sample (Algorithm R, k=1).
        /// Caller maintains shared 'selected' and 'totalCount' across multiple layers.
        /// Single-hyperedge: iterates directly, no dedup needed.
        /// Multi-hyperedge: deduplicates via a thread-local HashSet before sampling.
        /// Zero allocation per call.
        /// </summary>
        void AppendProjectedAltersReservoir(uint nodeId, ref uint? selected, ref int totalCount);
        #endregion
    }
}
