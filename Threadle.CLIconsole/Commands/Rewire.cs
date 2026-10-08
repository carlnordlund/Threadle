using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Processing;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'rewire' CLI command.
    /// </summary>
    public class Rewire : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "rewire(network = [var:network], layername = [str], *newlayername = [str], *numswaps = [int(default:0)], *swaptype = ['2edge'(default),'3edge'], *maxtries = [int(default:0)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Randomizes a 1-mode layer via repeated degree-preserving edge swaps (Markov chain switching), storing the result as a new layer with the same directionality, edge type and selfties setting as the original (left unmodified). Every node's degree (in- and out-degree separately, for directed layers) is preserved exactly. Edge values travel with the edge being repositioned: for directed layers each edge's original tail stays fixed as its head is reassigned; for symmetric layers, which endpoint of each edge is treated as fixed is chosen independently at random for every swap, since an undirected edge has no real direction to anchor a value to. 'swaptype' selects the swap mechanics for directed layers: '2edge' (default, matches igraph's convention) picks two arcs (a,b) and (c,d) and reconnects them as (a,d) and (c,b); '3edge' instead swaps along a 3-arc path a->b->c->d to a->c->b->d, matching networkx's directed_edge_swap, needed because '2edge' is not guaranteed to reach every digraph with the same in/out-degree sequence (though '3edge' never disturbs existing self-loops, since it only operates on 4-distinct-node paths). For symmetric layers, which have no such subtlety, 'swaptype' is ignored and '2edge' is always used. 'numswaps' is the target number of successful swaps (0, the default, uses 10 times the layer's edge count); 'maxtries' caps the total number of attempts, successful or not, so a layer too dense or structured to find enough valid swaps does not loop unboundedly (0, the default, uses 10 times numswaps). The new layer is named as the provided layer with the addition '-rewired', unless 'newlayername' is given. Reference: Maslov & Sneppen (2002) doi:10.1126/science.1065103.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console variable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            int numSwaps = command.GetArgumentParseInt("numswaps", 0);
            int maxTries = command.GetArgumentParseInt("maxtries", 0);
            string swapTypeStr = command.GetArgumentParseString("swaptype", "2edge");
            bool threeEdgeSwap;
            switch (swapTypeStr.ToLowerInvariant())
            {
                case "2edge": threeEdgeSwap = false; break;
                case "3edge": threeEdgeSwap = true; break;
                default: return CommandResult.Fail("InvalidArgument", $"Unknown swaptype '{swapTypeStr}': expected '2edge' or '3edge'.");
            }
            string newLayerName = network.GetNextAvailableLayerName(command.GetArgumentParseString("newlayername", layerName + "-rewired"));
            return CommandResult.FromOperationResult(NetworkProcessor.RewireLayer(network, layerName, newLayerName, numSwaps, threeEdgeSwap, maxTries));
        }
    }
}