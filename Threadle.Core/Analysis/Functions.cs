using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Analysis
{
    public static class Functions
    {
        internal static double Density(Network network, LayerOneMode layer)
        {
            ulong nbrPotentialEdges = GetNbrPotentialEdges((ulong)network.Nodeset.Count, layer.IsDirectional, layer.Selfties);
            ulong nbrExistingEdges = layer.NbrEdges;
            return (double)nbrExistingEdges / nbrPotentialEdges;
        }

        private static ulong GetNbrPotentialEdges(ulong n, bool isDirectional, bool selfties)
        {
            if (isDirectional)
                return selfties ? n * n : n * (n - 1);
            else
                return selfties ? n * (n + 1) / 2 : n * (n - 1) / 2;
        }
    }
}
