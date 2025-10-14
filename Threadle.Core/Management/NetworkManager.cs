using PopnetEngine.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Management
{
    /// <summary>
    /// Manager in Core that takes care of
    /// storing currently active NetworkModel objects
    /// providing access to these (by id),
    /// deleting/resetting networks, maybe also
    /// loading/saving network files
    /// This should be static
    /// THIS MAINLY USED FOR NativeAOT - where I need to keep track of states
    /// THE CLI/WPF SOLUTIONS KEEP THEIR OWN COLLECTIONS
    /// Note: NetworkManager should not be in NativeAOT, the latter
    /// is just a wrapper. Which will call into
    /// NetworkManager in Core
    /// Also: NetworkManager should not be bloated - only for keeping persistence of
    /// NetworkModel objects. When it comes to actual adding nodes etc, that should
    /// be done with the NetworkModel objects directly.
    /// Note: NetworkManager is static
    /// </summary>
    public static class NetworkManager
    {
        private static readonly Dictionary<int, Network> _networks = new();
        private static int _nextId = 1;

        public static int CreateNetwork()
        {
            int id = _nextId++;
            _networks[id]=new Network(string.Empty);
            return id;
        }

        public static Network? GetNetwork(int id)
        {
            return _networks.TryGetValue(id, out Network? network) ? network : null;
        }

        public static bool DeleteNetwork(int id)
        {
            return _networks.Remove(id);
        }

        public static void ClearAllNetworks()
        {
            _networks.Clear();
        }
    }
}
