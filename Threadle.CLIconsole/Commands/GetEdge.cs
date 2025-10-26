using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Threadle.CLIconsole.Commands
{
    public class GetEdge : ICommand
    {
        public string Usage => "[float] = getedge(network = [var:network], layername = [str], node1id = [uint], node2id = [uint])";

        public string Description => "Returns the edge value between node1id (from) and node2id (to) in the specified layer 'layername', which can be either 1-mode or 2-mode. If the layer is 1-mode directional, node1id is the source and node2id is the destination. If no edge is found, returns zero. For 2-mode layers, the value represents the number of affiliations that the two nodes share in this particular layer, i.e. the value that typically emerge when using the classical matrix-multiplcation-approach for projecting 2-mode data to 1-mode.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Core.Model.Network network = context.GetVariableThrowExceptionIfMissing<Core.Model.Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg2");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg3");
            OperationResult<float> result = network.GetEdge(layerName, node1id, node2id);
            if (result.Success)
                ConsoleOutput.WriteLine(result.Value.ToString(), true);
            else
                ConsoleOutput.WriteLine(result.ToString());



            //ConsoleOutput.WriteLine("Rewrite this",true);
            //string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0");
            //Network network = context.GetVariable<Network>(networkName)
            //    ?? throw new Exception($"!Error: No Network '{networkName}' found.");
            //Nodeset nodeset = network.Nodeset;
            //string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            //if (!network.Layers.TryGetValue(layerName, out var layer))
            //    throw new Exception($"!Error: Layer with name '{layerName}' not found.");
            //uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg2");
            //uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg3");
            //float val = network.GetEdgeValue(layerName, node1id, node2id);
            //ConsoleOutput.Write($"Value: ");
            //ConsoleOutput.WriteLine($"{val:0.####}", true);
        }
    }
}
