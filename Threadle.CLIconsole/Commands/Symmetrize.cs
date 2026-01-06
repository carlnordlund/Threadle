using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Processing;
using Threadle.Core.Processing.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class Symmetrize : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "symmetrize(network = [var:network], layername = [str], method = ['max'(default),'min','minnonzero','average','sum','product'], * newlayername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "[Description of what the command does].";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1").ToLowerInvariant();
            SymmetrizeMethod method = command.GetArgumentParseEnum<SymmetrizeMethod>("method", SymmetrizeMethod.max);
            string newLayerName = network.GetNextAvailableLayerName(command.GetArgumentParseString("newlayername", layerName + "-symmetrized").ToLowerInvariant());
            return CommandResult.FromOperationResult(NetworkProcessor.SymmetrizeLayer(network, layerName, method, newLayerName));


            return CommandResult.Fail("NotYetImplemented", "This command is not yet implemented");
        }
    }
}
