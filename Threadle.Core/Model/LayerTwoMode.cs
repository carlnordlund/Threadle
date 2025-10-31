using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Describes a layer where relations are 2-mode, i.e. consisting of HyperEdge objects
    /// which is thus non-projected 2-mode data
    /// One of these Layers could then describe workplaces
    /// In here is a collection of all HyperEdge objects (events/affiliations) that can exist
    /// reachable by keynote (such as name/id of workplace)
    /// </summary>
    public class LayerTwoMode : ILayer
    {
        public string Name { get; set; } = "";

        public uint NbrHyperedges { get => (uint)AllHyperEdges.Count; }

        // Dict to store collection of HyperEdge structs for each Node
        // So one HyperEdgeCollection per Node.
        // These collections contain all HyperEdge (affiliations) for the specific Node
        public Dictionary<uint, HyperEdgeCollection> HyperEdgeCollections = [];

        // Dict to store all HyperEdge objects (i.e. affiliations) in this layer.
        // This would then contain all HyperEdge objects (e.g. all workplaces/events) that
        // exist in this layer
        // Within a LayerTwoMode, HyperEdge objects must have separate names. Which makes sense
        public Dictionary<string, HyperEdge> AllHyperEdges = [];


        public Dictionary<string, object> GetMetadata => new()
        {
            ["Name"] = Name,
            ["Mode"] = 2,
            ["NbrHyperedges"] = NbrHyperedges
        };


        /// <summary>
        /// Constructor used by file loader
        /// </summary>
        public LayerTwoMode()
        {

        }

        public LayerTwoMode(string name)
        {
            Name = name;
        }


        /// <summary>
        /// Adds a Hyperedge with the specified name and array of nodeId members. Makes sure to filter out duplicte node Ids
        /// for the Hyperedge. If there is already a Hyperedge with this name, the old one is first removed
        /// </summary>
        /// <param name="hyperName">Name of hyperedge.</param>
        /// <param name="nodeIds">Optional array (uint[]) of node id's belonging to this hyperedge.</param>
        public OperationResult AddHyperedge(string hyperName, uint[]? nodeIds)
        {
            if (AllHyperEdges.ContainsKey(hyperName))
                RemoveHyperedge(hyperName);
            HyperEdge hyperedge = new();
            AllHyperEdges.Add(hyperName, hyperedge);
            if (nodeIds != null && nodeIds.Length > 0)
            {
                hyperedge.nodeIds = [.. nodeIds.Distinct()];
                foreach (uint nodeid in nodeIds)
                    AddHyperEdgeToNode(nodeid, hyperedge);
            }
            return OperationResult.Ok($"Added hyperedge '{hyperName}' (with {hyperedge.NbrNodes} nodes) in layer '{Name}'.");
        }

        /// <summary>
        /// Add a hyperedge reference to a node's collection of hyperedges. If the node has no such collection, it is
        /// first created.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="hyperEdge">The hyperedge that the node is part of</param>
        private void AddHyperEdgeToNode(uint nodeId, HyperEdge hyperEdge)
        {
            if (HyperEdgeCollections.TryGetValue(nodeId, out var collection))
                collection.AddHyperEdge(hyperEdge);
            else
                HyperEdgeCollections.Add(nodeId, new HyperEdgeCollection(hyperEdge));
        }

        public void RemoveHyperedge(string hyperName)
        {
            // 1. Try getting this hyperedge in AllHyperEdges. If not: return (do nothing)
            if (!AllHyperEdges.TryGetValue(hyperName, out var hyperedge))
                return;
            // 2. Remove this hyperedge from AllHyperEdges
            AllHyperEdges.Remove(hyperName);
            // If the hyperedge has nodes:
            if (hyperedge.nodeIds.Count > 0)
            {
                // 4. Iterate through all nodeIds in this hyperedge
                foreach (uint nodeId in hyperedge.nodeIds)
                    // 5. Remove any reference this node has to this Hyperedge
                    RemoveHyperEdgeFromNode(nodeId, hyperedge);
            }
        }

        public void RemoveNodeEdges(uint nodeId)
        {
            HyperEdgeCollections.Remove(nodeId);
            foreach (HyperEdge hyperedge in AllHyperEdges.Values)
                hyperedge.nodeIds.Remove(nodeId);
        }


        public float GetEdgeValue(uint sourceNodeId, uint targetNodeId)
        {
            if (!HyperEdgeCollections.TryGetValue(sourceNodeId, out var sourceCollection) || !HyperEdgeCollections.TryGetValue(targetNodeId, out var targetCollection))
                return 0f;
            return (sourceCollection.HyperEdges.Intersect(targetCollection.HyperEdges)).Count();
        }

        public bool CheckEdgeExists(uint sourceNodeId, uint targetNodeId)
        {
            if (!HyperEdgeCollections.TryGetValue(sourceNodeId, out var hyperEdgeCollection))
                return false;
            foreach (HyperEdge hyperedge in hyperEdgeCollection.HyperEdges)
                if (hyperedge.nodeIds.Contains(targetNodeId))
                    return true;
            return false;
        }

        public uint[] GetAlterIds(uint nodeId, EdgeTraversal edgeTraversal)
        {
            if (!HyperEdgeCollections.TryGetValue(nodeId, out var hyperEdgeCollection) || hyperEdgeCollection.HyperEdges.Count == 0)
                return [];

            HashSet<uint> alters = [];
            foreach (HyperEdge hyperEdge in hyperEdgeCollection.HyperEdges)
                alters.UnionWith(hyperEdge.nodeIds);
            alters.Remove(nodeId);
            return [.. alters];
        }

        private void RemoveHyperEdgeFromNode(uint nodeId, HyperEdge hyperEdge)
        {
            if (HyperEdgeCollections.TryGetValue(nodeId, out var collection))
                collection.HyperEdges.Remove(hyperEdge);
        }

        public HashSet<uint> GetMentionedNodeIds()
        {
            HashSet<uint> ids = [];
            foreach (HyperEdge hyperEdge in AllHyperEdges.Values)
            {
                ids.UnionWith(hyperEdge.nodeIds);
            }
            return ids;
        }

        public void ClearLayer()
        {
            HyperEdgeCollections.Clear();
            foreach (HyperEdge hyperedge in AllHyperEdges.Values)
                hyperedge.Clear();
            AllHyperEdges.Clear();
        }
    }
}
