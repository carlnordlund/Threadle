using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    public class RemoveHyper : ICommand
    {
        public string Usage => "removehyper(network = [var:network], layername = [str], hypername = [str])";

        public string Description => "Removes the hyperedge 'hypername' from layer 'layername' in the specified network.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string hyperName = command.GetArgumentThrowExceptionIfMissingOrNull("hypername", "arg2");
            ConsoleOutput.WriteLine(network.RemoveHyperedge(layerName, hyperName).ToString());
        }

    }
}
