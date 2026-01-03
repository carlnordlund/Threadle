using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'generate' CLI command.
    /// </summary>
    public class GenerateRandom : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "generate(network = [var:network], layername = [str], type = ['er'], p = [double])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates a random network of the specified type (only Erdös-Renyi implemented so far) in the specified layer of the specified network. The layer must already exist and be binary: any would-be ties that exist in that layer will first be removed.";

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
            string type = command.GetArgumentThrowExceptionIfMissingOrNull("type", "arg2");
            double p = command.GetArgumentParseDoubleThrowExceptionIfMissingOrNull("p", "arg3");
            if (type.Equals("er"))
                return CommandResult.FromOperationResult(NetworkGenerators.ErdosRenyiLayer(network, layerName, p));
            else
                return CommandResult.Fail("TypeNotFound", $"Random network type '{type}' not recognized.");
        }
    }
}
