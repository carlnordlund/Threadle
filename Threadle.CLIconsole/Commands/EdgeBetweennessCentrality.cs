using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'edgebetweennesscentrality' CLI command.
    /// </summary>
    public class EdgeBetweennessCentrality : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "edgebetweennesscentrality(network = [var:network], layername = [str], *newlayername = [str], *samplesize = [int(default:0)], *normalize = ['true'(default),'false'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates edge betweenness centrality for every edge in the specified 1-mode layer using Brandes' algorithm, storing the result as a new valued layer with the same directionality as the source layer. When samplesize > 0, a random subset of source nodes is used and scores are scaled accordingly, same as betweennesscentrality(). When normalize is true (default), scores are divided by (n-1)(n-2) for directed layers, (n-1)(n-2)/2 for undirected; set to false for raw path-count scores. The per-source BFS passes run in parallel across up to 'maxthreads' threads (see 'setting()'). The new layer is named as the provided layer, with the addition '-edgebetweenness', but the name of the new layer can also be specified with the 'newlayername' argument. Kept as a separate command from betweennesscentrality() since edge identity, unlike node identity, has no clean mapping across combined layers — this only operates on a single named layer. See Brandes (2001); sampling: Brandes & Pich (2007).";

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
            int sampleSize = command.GetArgumentParseInt("samplesize", 0);
            bool normalize = command.GetArgumentParseBool("normalize", true);
            string newLayerName = network.GetNextAvailableLayerName(command.GetArgumentParseString("newlayername", layerName + "-edgebetweenness"));
            return CommandResult.FromOperationResult(Analyses.EdgeBetweennessCentrality(network, layerName, newLayerName, sampleSize, normalize));
        }
    }
}