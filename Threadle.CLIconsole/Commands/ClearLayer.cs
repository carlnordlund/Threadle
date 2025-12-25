using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'clearlayer' CLI command.
    /// </summary>
    public class ClearLayer : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "clearlayer(network = [var:network], layername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Removes all edges in the specified layer for the specified network (but keeps the layer). For 2-mode layers, this therefore removes all hyperedges.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(Command command, CommandContext context)
        {
            //string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0");
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            return CommandResult.FromOperationResult(network.ClearLayer(layerName));
            //ConsoleOutput.WriteLine(network.ClearLayer(layerName).ToString());
        }
    }
}