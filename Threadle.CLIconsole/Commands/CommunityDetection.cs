using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'communitydetection' CLI command.
    /// </summary>
    public class CommunityDetection : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "communitydetection(network=[var:network], *layernames=[semicolon-separated], *attrname=[str], *resolution=[double])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Detects communities via Louvain modularity optimization, storing the community index as an integer node attribute. Directed layers are symmetrized (arcs in both directions accumulate into an undirected weight) since modularity optimization is inherently undirected. Multiple layers combine via summed edge weight. Only 1-mode layers are accepted — project 2-mode layers first with projecttwomodetoonemode(). resolution (default 1.0) scales the null-model term: values above 1 favor more, smaller communities; values below 1 favor fewer, larger ones. Returns NbrCommunities, CommunitySizes (descending) and the achieved Modularity score. Reference: Blondel, Guillaume, Lambiotte & Lefebvre (2008) doi:10.1088/1742-5468/2008/10/P10008.";

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
            string layerNames = command.GetArgumentParseString("layernames", "");
            string? attrName = command.GetArgumentParseString("attrname", "");
            double resolution = command.GetArgumentParseDouble("resolution", 1.0);
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;
            string? attr = attrName.Length > 0 ? attrName : null;
            var result = Analyses.CommunityDetection(network, layers, attr, resolution);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
