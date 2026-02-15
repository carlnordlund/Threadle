using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'addlayer' CLI command.
    /// </summary>
    public class AddLayer : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "addlayer(network = [var:network], layername = [str], mode = ['1','2'], *directed = ['true','false'(default)], *valuetype = ['binary'(default),'valued'], *selfties=['true','false'(default)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Adds a relational layer for a network, which can be either 1-mode or 2-mode (with hyperedges). For 1-mode layers, there are additional settings for the type of ties that exist, specifying edge directionality, edge value type and whether selfties are allowed or not. For 2-mode layer, these settings do not matter: all ties are deemed binary. A network must have at least one layer defined, referred to when adding edges between nodes and nodes-affiliations as well as when importing edges from file to a layer.";

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
            if (!command.TrimNameAndCheckValidity(layerName, out string layerNameVerified))
                return CommandResult.Fail("InvalidLayerName", $"Layer name '{layerName}' is not valid. It must start with a letter and contain only letters, digits, and underscores.");
            char mode = command.GetArgumentThrowExceptionIfMissingOrNull("mode", "arg2")[0];
            OperationResult result;
            if (mode == '1')
                result = network.AddLayerOneMode(
                    layerName: layerNameVerified,
                    edgeDirectionality: command.GetArgumentParseBool("directed", false) ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected,
                    edgeValueType: command.GetArgumentParseEnum<EdgeType>("valuetype", EdgeType.Binary),
                    selfties: command.GetArgumentParseBool("selfties", false)
                    );
            else if (mode == '2')
                result = network.AddLayerTwoMode(layerNameVerified);
            else
                return CommandResult.Fail("InvalidMode", $"Unknown mode ('{mode}') - must be either '1' or '2'.");
            return CommandResult.FromOperationResult(result);
        }
    }
}
