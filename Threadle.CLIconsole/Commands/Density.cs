using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'density' CLI command.
    /// </summary>
    public class Density : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[double] = density(network = [var:network], layername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates and returns the density of layer 'layername' of the specified network, up to 8 decimals. Treats all existing ties as binary ties.";

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
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            var densityResult = Analyses.Density(network, layerName);
            if (densityResult.Success)
                return CommandResult.Ok(densityResult.Message, densityResult.Value);
            //ConsoleOutput.WriteLine($"{densityResult.Value:0.0000####}", true);
            else
                return CommandResult.Fail(densityResult.Code, densityResult.Message);
                //ConsoleOutput.WriteLine(densityResult.ToString());
        }
    }
}
