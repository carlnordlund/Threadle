using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Processing.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class ProjectTwoMode : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "projecttwomode(network = [var:network], layername = [str], *method = ['count'(default),'newman','binary'], *newlayername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Projects the specified 2-mode layer in the specified network. The layer must be 2-mode. Projection method is by default 'count', which results in valued symmetric edges between nodes representing how many affiliations pairs of nodes share. The 'binary' projection method builds on 'count', but dichotomzies the results into binary ties (and thus also a binary layer). The 'newman' projection method normalizes the contribution of each hyperedge by its size (see Newman 2001. Scientific collaboration networks. Physical Review E). A new symmetrized 1-mode layer is created: the original 2-mode layer will remain as it is. An optional name for the new layer can be specified: otherwise, it will be automatically named.";

        //Symmetrize the specified layer in the specific network. The layer must be 1-mode and preferably directional. A new, symmetrized version of the layer will be created: the original layer will remain as it is. An optional name for the new layer can be specified: otherwise, it will be automatically named.

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
            ProjectionMethod method = command.GetArgumentParseEnum<ProjectionMethod>("method", ProjectionMethod.Count);
            string newLayerName = network.GetNextAvailableLayerName(command.GetArgumentParseString("newlayername", layerName + "-projected"));
            return CommandResult.FromOperationResult(NetworkProcessor.ProjectTwoModeToOneMode(network, layerName, method, newLayerName));
        }
    }
}
