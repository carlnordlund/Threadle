using PopnetEngine.Core.Analysis;
using PopnetEngine.Core.Model;
using PopnetEngine.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.CLIconsole.Commands
{
    public class ShortestPathCommand : ICommand
    {
        public string Usage => "[var:matrix] = shortestpaths(network=[var:network], layername=[str])";
        public string Description => "Calculates the shortest path between each pair of Nodes in the given network and layer.";
        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(true);
            string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0");
            Network network = context.GetVariable<Network>(networkName)
                ?? throw new Exception($"!Error: No Network '{networkName}' found.");
            byte layerId = (byte)command.GetArgumentParseInt("layerid", 0);
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string assignedVariable = command.AssignedVariable!;

            if (!(Analyses.ShortestPaths(network, layerName) is MatrixStructure shortestpaths))
                throw new Exception($"!Error: Something went wrong when calculating shortest paths.");
                
            context.SetVariable(assignedVariable, shortestpaths);
            ConsoleOutput.WriteLine($"Stored shortest path matrix in variable {assignedVariable}.");
        }
    }
}
