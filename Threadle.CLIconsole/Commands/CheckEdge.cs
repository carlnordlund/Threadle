using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class CheckEdge : ICommand
    {
        public string Usage => "[bool] = checkedge(network = [var:network], layername = [str], node1id = [int], node2id = [int])";

        public string Description => "Checks if an edge exists between node1id (from) and node2id (to) in the specified layer 'layername', which can be either 1-mode or 2-mode. If the layer is 1-mode directional, node1id is the source and node2id is the destination. Returns the string 'true' if an edge is found, otherwise 'false'. For 2-mode layers, an edge exists if the two nodes share at least one affiliation (hyperedge).";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Core.Model.Network network = context.GetVariableThrowExceptionIfMissing<Core.Model.Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg2");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg3");
            OperationResult<bool> result = network.CheckEdgeExists(layerName, node1id, node2id);
            if (result.Success)
                ConsoleOutput.WriteLine(Misc.BooleanAsString(result.Value), true);
            else
                ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
