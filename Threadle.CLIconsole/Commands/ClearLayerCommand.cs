using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    public class ClearLayerCommand : ICommand
    {
        public string Usage => "clearlayer(network = [var:network], layername = [str])";

        public string Description => "Removes all edges in the specified layer for the specified network (but keeps the layer). For 2-mode layers, this therefore removes all hyperedges.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            ConsoleOutput.WriteLine(network.ClearLayer(layerName).ToString());

        }
    }
}