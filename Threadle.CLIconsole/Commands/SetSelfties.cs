using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'setselfties' CLI command.
    /// </summary>
    public class SetSelfties : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "setselfties(network = [var:network], layername = [str], *selfties = ['true'(default),'false'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Sets the selfties property of an existing 1-mode layer in a network, controlling whether edges from a node to itself are permitted. Does not add or remove any existing self-tie edges — only the layer property is updated. Applies only to unpacked 1-mode layers; returns an error for 2-mode or packed (static) layers.";

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
            bool selfties = command.GetArgumentParseBool("selfties", true);

            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return CommandResult.Fail(layerResult.Code, layerResult.Message);

            ILayer layer = layerResult.Value!;
            if (layer is not ILayerOneMode)
                return CommandResult.Fail("InvalidLayerType", $"Layer '{layerName}' is not a 1-mode layer. The setselfties command only applies to 1-mode layers.");
            if (layer.IsStatic)
                return CommandResult.Fail("LayerIsPacked", $"Layer '{layerName}' is packed (static). Unpack it first with unpack() before changing its properties.");

            ((LayerOneMode)layer).Selfties = selfties;
            return CommandResult.Ok($"Selfties set to '{selfties}' for layer '{layerName}' in network '{network.Name}'.");
        }
    }
}
