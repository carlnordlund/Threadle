using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

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

        // Method for AddAffiliation
        //internal void AddAffiliation(uint nodeId, string affiliationLabel)
        //{
        //    // Get or create HyperEdgeCollection for this nodeId
        //    // Get or create HyperEdge for this affiliationLabel (i.e. if create: store the new HyperEdge in AllHyperEdges)
        //    // Add nodeId to the HyperEdge
        //    // Add the hyperEdge to the HyperEdgeCollection for this nodeId
        //}

        /// <summary>
        /// Adds a Hyperedge with the specified name and array of nodeId members
        /// </summary>
        /// <param name="hyperName">Name of hyperedge.</param>
        /// <param name="nodeIds">Array (uint[]) of node id's belonging to this hyperedge.</param>
        public OperationResult AddHyperedge(string hyperName, uint[]? nodeIds)
        {
            // If there is already a HyperEdge with this name, first remove it (including all pointers to it)
            if (AllHyperEdges.ContainsKey(hyperName))
                RemoveHyperedge(hyperName);
            // Create hyperedge and possibly initialize it with nodeIds
            HyperEdge hyperedge = new HyperEdge();
            AllHyperEdges.Add(hyperName, hyperedge);
            if (nodeIds != null && nodeIds.Length > 0)
            {
                hyperedge.nodeIds = nodeIds.ToList();
                foreach (uint nodeid in nodeIds)
                    AddHyperEdgeToNode(nodeid, hyperedge);
            }
            return OperationResult.Ok($"Added hyperedge '{hyperName}' (with {hyperedge.NbrNodes} nodes) in layer '{Name}'.");
        }

        //public void AddHyperedge(string hyperName)
        //{
        //    AddHyperedge(hyperName, new HyperEdge());
        //}

        //public void AddHyperedge(string hyperName, HyperEdge hyperedge)
        //{
        //    if (AllHyperEdges.ContainsKey(hyperName))
        //        RemoveHyperedge(hyperName);
        //    AllHyperEdges[hyperName] = hyperedge;
        //    if (hyperedge.nodeIds.Count > 0)
        //        foreach (uint nodeId in hyperedge.nodeIds)
        //            AddHyperEdgeToNode(nodeId, hyperedge);
        //}

        private void AddHyperEdgeToNode(uint nodeId, HyperEdge hyperEdge)
        {
            if (HyperEdgeCollections.TryGetValue(nodeId, out var collection))
                collection.AddHyperEdge(hyperEdge);
            else
                HyperEdgeCollections.Add(nodeId, new HyperEdgeCollection(hyperEdge));
        }

        public void RemoveHyperedge(string hyperName)
        {
            // 1. Check if there is a hyperedge in AllHyperEdges. If not: return (do nothing)
            if (!AllHyperEdges.TryGetValue(hyperName, out var hyperEdge))
                return;
            // 2. take ref to this HyperEdge
            // 3. Remove from AllHyperEdges
            AllHyperEdges.Remove(hyperName);
            // If the hyperedge has nodes:
            if (hyperEdge.nodeIds.Count > 0)
            {
                // 4. Iterate through all nodeIds in this hyperedge
                foreach (uint nodeId in hyperEdge.nodeIds)
                    // 5. Remove any reference this node has to this Hyperedge
                    RemoveHyperEdgeFromNode(nodeId, hyperEdge);
            }
        }

        public float GetEdgeValue(uint sourceNodeId, uint targetNodeId)
        {
            if (!HyperEdgeCollections.TryGetValue(sourceNodeId, out var sourceCollection) || !HyperEdgeCollections.TryGetValue(targetNodeId, out var targetCollection))
                return 0f;
            return (sourceCollection.HyperEdges.Intersect(targetCollection.HyperEdges)).Count();

            //if (!HyperEdgeCollections.TryGetValue(sourceNodeId, out var hyperEdgeCollection))
            //    return 0f;
            //int count = 0;
            //foreach (HyperEdge hyperedge in hyperEdgeCollection.HyperEdges)
            //    count += (hyperedge.nodeIds.Contains(targetNodeId)) ? 1 : 0;
            //return count;
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
                return Array.Empty<uint>();
            //if (hyperEdgeCollection.HyperEdges.Count == 1)
            //{
            //    var hyperEdge = hyperEdgeCollection.HyperEdges[0];
            //}


            HashSet<uint> alters = new();
            foreach (HyperEdge hyperEdge in hyperEdgeCollection.HyperEdges)
                alters.UnionWith(hyperEdge.nodeIds);
            alters.Remove(nodeId);
            return alters.ToArray();
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

        }
    }
}
