using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getrandomalter' CLI command.
    /// </summary>
    public class GetRandomAlter : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[uint] = getrandomalter(network = [var:network], nodeid = [uint], *layernames = [semicolon-separated layer names], *direction = ['both'(default),'in','out'], *balanced = ['true','false'(default)], *weighted = ['false'(default),'true'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Get the node id of a random alter to the specified node. By default, both in- and outbound ties are considered, but this can be adjusted. By default, the pick is randomly picked among all layers, but one or more layers to use can be specified with the 'layernames' argument. If more than one layer is included, the 'balanced' argument specifies how the pick should be done. If balanced is set to 'true', a uniformly-randomly picked layer takes place first, followed by a random pick of an alter in the specific layer that was picked. If set to 'false', alters in all layers are first pooled together (with the possibility of an alter appearing multiple times) and a random pick is then done among this complete set of alters across layers. If 'weighted' is set to 'true', edge weights are used as transition probabilities; for binary layers each alter is treated as having weight 1.0f.\r\n";

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
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string layerNamesRaw = command.GetArgumentParseString("layernames", "");
            string[]? layerNames = layerNamesRaw.Length > 0 ? layerNamesRaw.Split(';') : null;
            EdgeTraversal edgeTraversal = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.Both);
            bool balanced = command.GetArgumentParseBool("balanced", false);
            bool weighted = command.GetArgumentParseBool("weighted", false);
            OperationResult<uint> result = Analyses.GetRandomAlter(network, nodeId, layerNames, edgeTraversal, balanced, weighted);
            return CommandResult.FromOperationResult(
                opResult: result,
                payload: result.Value
                );
        }
    }
}
