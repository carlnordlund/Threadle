using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core
{
    public static class UserSettings
    {
        /// IMPLEMENTED - Triggers in Nodeset.NodeArray property
        /// 
        /// <summary>
        /// Uses a separate (internal) lazy-initialized NodeCache array with all nodes
        /// to quickly obtain random Node, for NetworkGenerator etc
        /// </summary>
        public static bool NodeCache { get; set; } = true;

        /// IMPLEMENTED - Triggers in Network.AddEdge()
        /// 
        /// <summary>
        /// Validates that nodes involved in AddEdges actually exist in the
        /// Nodeset of the Network
        /// </summary>
        //public static bool ValidateNodes { get; set; } = true;

        /// IMPLEMENTED - Triggers in IEdgeset implementations
        /// 
        /// <summary>
        /// Checks that there are no existing edge when trying to add new Edge
        /// </summary>
        public static bool BlockMultiedges { get; set; } = true;

        /// IMPLEMENTED - Triggers in LayerOneMode.AddEdge()
        /// 
        /// <summary>
        /// Only stores outbound edges for directed layers, i.e. so that nodes
        /// lack references to inbound edges. System-wide property.
        /// </summary>
        public static bool OnlyOutboundEdges { get; set; } = false;

        public static OperationResult Set(string key, bool value)
        {
            if (key.Equals("nodecache"))
                NodeCache = value;
            else if (key.Equals("blockmultiedges"))
                BlockMultiedges = value;
            else if (key.Equals("onlyoutboundedges"))
                OnlyOutboundEdges = value;
            else
                return OperationResult.Fail("SettingNotFound", $"Unknown setting: '{key}'.");
            return OperationResult.Ok($"Setting '{key}' to {value}.");
        }
    }
}
