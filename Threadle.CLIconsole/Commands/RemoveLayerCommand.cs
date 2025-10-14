using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class RemoveLayerCommand : ICommand
    {
        public string Usage => "removelayer(network = [var:network], layername = [str])";

        public string Description => "Removes the relational layer 'layername' and all edges in that layer for the specified network.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            ConsoleOutput.WriteLine(network.RemoveLayer(layerName).ToString());
        }
    }
}
