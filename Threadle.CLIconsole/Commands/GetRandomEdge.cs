using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getrandomedge' CLI command.
    /// </summary>
    public class GetRandomEdge : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[int,int,float] = getrandomedge(network = [var:network], layername = [str], *maxattempts=[int(100)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Returns a random edge from the specified network and layer. While this is easy to do for 1-mode layers, the 2-mode layers samples random (pseudo-projected) node-to-node edges a bit differenlty. It first tries an approach where it randomly picks two nodes and checks if there is an edge between these two (i.e. if they share at least one affiliation). If this has been tried 100 times (adjusted with 'maxattempts') without finding an edge, the 2-mode heuristic then switches to a different kind of search that is slightly biased. First, it picks a random hyperedge based on their estimated projected weights, this being the size of the total graph that this hyperedge would generate if projected. Then it picks two random nodes from this hyperedge. The bias from this latter approach is that it is more likely to pick edges between nodes that share many affiliations.";

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
            int maxAttempts = command.GetArgumentParseInt("maxattempts", 100);
            OperationResult<Dictionary<string, object>> result = Analyses.GetRandomEdge(network, layerName, maxAttempts);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
