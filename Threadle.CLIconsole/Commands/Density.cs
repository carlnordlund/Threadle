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
    public class Density : ICommand
    {
        public string Usage => "[double] = density(network = [var:network], layername = [str])";

        public string Description => "Calculates the density of layer 'layername' of the specified network, up to 8 decimals. Treats all existing ties as binary ties.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Core.Model.Network network = context.GetVariableThrowExceptionIfMissing<Core.Model.Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            var densityResult = Analyses.Density(network, layerName);
            if (densityResult.Success)
                ConsoleOutput.WriteLine($"{densityResult.Value:0.0000####}", true);
            else
                ConsoleOutput.WriteLine(densityResult.ToString());
        }
    }
}
