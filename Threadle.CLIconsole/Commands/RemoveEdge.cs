using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    public class RemoveEdge : ICommand
    {
        public string Usage => "removeedge(network = [var:network], layername = [str], node1id = [uint], node2id = [uint])";

        public string Description => "Removes the edge (if one exists) between node1id and node2id in the specified layer 'layername' (which must be 1-mode) in network 'network'. If the layer is directional, node1id is the source (from) and node2id is the destination (to). Gives a warning message if any of the nodes are missing, or if the edge does not exist.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1").ToLowerInvariant();
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg2");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg3");
            ConsoleOutput.WriteLine(network.RemoveEdge(layerName, node1id, node2id).ToString());
        }
    }
}
