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
        public string Syntax => "generate(network = [var:network], layername = [str], type = ['er','ws'], +p = [double], +k = [int], +beta = [double])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates a random network of the specified type in the specified layer of the specified network. The layer must already exist and be binary: any would-be ties that exist in that layer will first be removed. Erdös-Renyi (type to 'er'): layer can be either directional or symmetric, with or without selfties; set p to the edge probability (which is also the overall network density). For Watts-Strogatz (type to 'ws'): layer must be symmetric; set k to mean degree (must be even), and beta to rewiring probability (0-1 range). Note that the arguments marked with (+) are compulsory arguments for each type and must be named; arguments for types not used can be ignored.";

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
            string type = command.GetArgumentThrowExceptionIfMissingOrNull("type", "arg2").ToLowerInvariant();
            switch (type) {
                case "er":
                    double p = command.GetArgumentParseDoubleThrowExceptionIfMissingOrNull("p", null);
                    return CommandResult.FromOperationResult(NetworkGenerators.ErdosRenyiLayer(network, layerName, p));
                case "ws":
                    int k = command.GetArgumentParseIntThrowExceptionIfMissingOrNull("k", null);
                    double beta = command.GetArgumentParseDoubleThrowExceptionIfMissingOrNull("beta", null);
                    return CommandResult.FromOperationResult(NetworkGenerators.WattsStrogatzLayer(network, layerName, k, beta));
                default:
                    return CommandResult.Fail("TypeNotFound", $"Random network type '{type}' not recognized.");

            }
            //double p = command.GetArgumentParseDoubleThrowExceptionIfMissingOrNull("p", "arg3");
            //if (type.Equals("er"))
            //    return CommandResult.FromOperationResult(NetworkGenerators.ErdosRenyiLayer(network, layerName, p));
            //else if (type.Equals("ws"))
            //    return CommandResult.FromOperationResult(NetworkGenerators.ErdosRenyiLayer(network, layerName, p));
            //else
            //    return CommandResult.Fail("TypeNotFound", $"Random network type '{type}' not recognized.");
        }
    }
}
