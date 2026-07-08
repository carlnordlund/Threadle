using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Processing;
using Threadle.Core.Processing.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'mergelayers' CLI command.
    /// </summary>
    public class MergeLayers : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "mergelayers(network = [var:network], layer1 = [str], layer2 = [str], *method = ['and','or','xor','sum','average','max'(default),'min','product'], *newlayername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Merges two 1-mode layers into a new layer by the provided method: the original layers remain unchanged. Both layers must have the same directionality (both directed or both undirected): symmetrize the directed one first with symmetrize() if they don't match. The method fully determines the resulting layer's edge type: 'and'/'or'/'xor' always produce a binary layer (does a tie exist, ignoring its strength); 'sum'/'average' always produce a valued layer; 'max'/'min'/'product' produce a binary layer only if both source layers are binary, valued otherwise. For binary layers, 'and' is equivalent to 'product' and 'or' is equivalent to 'max'; 'xor' (true only if exactly one layer has the tie) has no equivalent among the others. An optional name for the new layer can be specified: otherwise, it will be automatically named.";

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
            string layerName1 = command.GetArgumentThrowExceptionIfMissingOrNull("layer1", "arg1");
            string layerName2 = command.GetArgumentThrowExceptionIfMissingOrNull("layer2", "arg2");
            MergeMethod method = command.GetArgumentParseEnum<MergeMethod>("method", MergeMethod.Max);
            string newLayerName = network.GetNextAvailableLayerName(command.GetArgumentParseString("newlayername", layerName1 + "-" + layerName2 + "-merged"));
            return CommandResult.FromOperationResult(NetworkProcessor.MergeLayers(network, layerName1, layerName2, method, newLayerName));
        }
    }
}
