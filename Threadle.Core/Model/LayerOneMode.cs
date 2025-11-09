using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public class LayerOneMode : ILayer
    {

        public string Name { get; set; } = "";

        //public string Name = "";
        public EdgeDirectionality Directionality;
        public EdgeType EdgeValueType;
        public bool Selfties;

        Func<IEdgeset>? edgeSetFactory;



        public bool IsSymmetric => Directionality == EdgeDirectionality.Undirected;
        public bool IsDirectional => Directionality == EdgeDirectionality.Directed;
        public bool IsBinary => EdgeValueType == EdgeType.Binary;
        public bool IsValued => EdgeValueType == EdgeType.Valued;
        

        public ulong NbrEdges
        {
            get
            {
                uint nbrConnections = 0;
                foreach ((uint nodeId, IEdgeset edgeset) in Edgesets)
                    nbrConnections += edgeset.NbrEdges;
                return (Directionality == EdgeDirectionality.Directed ? nbrConnections : nbrConnections / 2);
            }
        }

        public Dictionary<string, object> GetMetadata => new()
        {
            ["Name"] = Name,
            ["Mode"] = 1,
            ["Directionality"] = Directionality.ToString(),
            ["ValueType"] = EdgeValueType.ToString(),
            ["SelftiesAllowed"] = Selfties,
            ["NbrEdges"] = NbrEdges
        };

        public Dictionary<uint, IEdgeset> Edgesets = [];

        public LayerOneMode()
        {
            // Factory isn't initialized after loading, need to do that!
            // Regarding sentence above: Hm, no, this is used by FileSerializerTsv, and factory
            // is initialized!
        }

        public LayerOneMode(string name, EdgeDirectionality directionality, EdgeType valueType, bool selfties)
        {
            Name = name;
            Directionality = directionality;
            EdgeValueType = valueType;
            Selfties = selfties;
            InitializeFactory();
        }

        private void InitializeFactory()
        {
            if (IsBinary)
            {
                if (IsDirectional)
                    edgeSetFactory = () => new EdgesetBinaryDirectional();
                else
                    edgeSetFactory = () => new EdgesetBinarySymmetric();
            }
            else if (IsValued) {
                if (IsDirectional)
                    edgeSetFactory = () => new EdgesetValuedDirectional();
                else
                    edgeSetFactory = () => new EdgesetValuedSymmetric();
            }
        }

        /// <summary>
        /// Method for adding edge to this 1-mode layer. It is here assumed that node1id and node2id have
        /// been previously verified, i.e. that they belong to the Nodeset in question.
        /// Checks for self-ties.
        /// This is the final bottleneck for adding edges: where all addedge() roads lead (e.g. from creating
        /// programmatically, as well as loading etc).
        /// </summary>
        /// <param name="node1Id"></param>
        /// <param name="node2Id"></param>
        /// <param name="value"></param>
        internal OperationResult AddEdge(uint node1Id, uint node2Id, float value = 1)
        {
            if (node1Id == node2Id && !Selfties)
                return OperationResult.Fail("SelftiesNotAllowed",$"Layer {Name} does not allow for selfties.");
            IEdgeset edgeSetNode1 = GetOrCreateEdgeset(node1Id);
            IEdgeset edgeSetNode2 = GetOrCreateEdgeset(node2Id);
            // Add inbound edge as long as OnlyOutboundEdges is not set to true
            if (!UserSettings.OnlyOutboundEdges)
                if (!edgeSetNode2.AddInboundEdge(node1Id, value).Success)
                    return OperationResult.Fail("EdgeAlreadyExist", $"Inbound edge to {node2Id} from {node1Id} already exists.");
            if (!edgeSetNode1.AddOutboundEdge(node2Id, value).Success)
                return OperationResult.Fail("EdgeAlreadyExist", $"Outbound edge from {node1Id} to {node2Id} already exists.");
            //string msg = Directionality == EdgeDirectionality.Directed ? $"from {node1Id} to {node2Id}" : $"between {node1Id} and {node2Id}";
            //string msg = Misc.BetweenFromToText(Directionality, node1Id, node2Id);
            return OperationResult.Ok($"Added edge {Misc.BetweenFromToText(Directionality, node1Id, node2Id)} (value={value}) in layer '{Name}'.");
        }

        internal OperationResult RemoveEdge(uint node1id, uint node2id)
        {
            if (!Edgesets.TryGetValue(node1id, out var edgesetNode1) || !Edgesets.TryGetValue(node2id, out var edgesetNode2))
                return OperationResult.Fail("EdgeNotFound", $"No edge {Misc.BetweenFromToText(Directionality, node1id, node2id)} in layer '{Name}' found.");
            if (!edgesetNode1.RemoveOutboundEdge(node2id).Success || !edgesetNode2.RemoveInboundEdge(node1id).Success)
                return OperationResult.Fail("EdgeNotFound", $"No edge {Misc.BetweenFromToText(Directionality, node1id, node2id)} in layer '{Name}' found.");
            return OperationResult.Ok($"Removed edge {Misc.BetweenFromToText(Directionality, node1id, node2id)} in layer '{Name}'.");
        }

        public void RemoveNodeEdges(uint nodeId)
        {
            // Remove any Edgeset that this node might have: none of these should be there
            Edgesets.Remove(nodeId);
            // Iterate through other nodes' Edgesets and prune out their 
            foreach (var edgeset in Edgesets.Values)
                edgeset.RemoveNodeEdges(nodeId);
        }

        private IEdgeset GetOrCreateEdgeset(uint nodeId)
        {
            if (Edgesets.TryGetValue(nodeId, out var edgeset))
                return edgeset;
            IEdgeset newEdgeset= edgeSetFactory!();
            Edgesets[nodeId] = newEdgeset;
            return newEdgeset;
        }

        //private IConnectionCollection GetOrCreateCollection(uint nodeId)
        //{
        //    if (Connections.TryGetValue(nodeId, out var collectionNode))
        //        return collectionNode;
        //    IConnectionCollection newCollection = Directionality == EdgeDirectionality.Directed ? new DirectedConnectionCollection() : new UndirectedConnectionCollection();
        //    Connections[nodeId] = newCollection;
        //    return newCollection;
        //}

        public float GetEdgeValue(uint sourceNodeId, uint targetNodeId)
        {
            if (!Edgesets.TryGetValue(sourceNodeId, out var edgeset))
                return 0f;
            return edgeset.GetOutboundPartnerEdgeValue(targetNodeId);
        }

        public bool CheckEdgeExists(uint sourceNodeId, uint targetNodeId)
        {
            if (!Edgesets.TryGetValue(sourceNodeId, out var edgeset))
                return false;
            return edgeset.CheckOutboundPartnerEdgeExists(targetNodeId);
        }

        public uint[] GetAlterIds(uint nodeId, EdgeTraversal edgeTraversal)
        {
            if (!(Edgesets.TryGetValue(nodeId, out var edgeset)))
                return [];

            return edgeset.GetAlterIds(edgeTraversal);
        }


        internal void TryInitFactory()
        {
            InitializeFactory();
        }

        public void ClearLayer()
        {
            foreach (IEdgeset edgeset in Edgesets.Values)
                edgeset.ClearEdges();
            Edgesets.Clear();
        }


        public HashSet<uint> GetMentionedNodeIds()
        {
            HashSet<uint> ids = [];
            foreach (IEdgeset edgeset in Edgesets.Values)
            {
                ids.UnionWith(edgeset.GetAllNodeIds);
            }
            return ids;
        }

        internal uint GetOutDegree(uint nodeId)
        {
            if (!Edgesets.TryGetValue(nodeId, out var edgeset))
                return 0;
            return edgeset.NbrOutboundEdges;
        }

        internal uint GetInDegree(uint nodeId)
        {
            if (!Edgesets.TryGetValue(nodeId, out var edgeset))
                return 0;
            return edgeset.NbrInboundEdges;
        }
    }
}
