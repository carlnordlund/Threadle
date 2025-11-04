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
    public class AddEdge : ICommand
    {
        public string Usage => "addedge(network = [var:network], layername = [str], node1id = [uint], node2id = [uint], *value = [float (default:1)], *addmissingnodes = ['true'(default), 'false'])";

        public string Description => "Creates and adds an edge between node1id and node2id in the specified layer 'layername' (which must be 1-mode) in network 'network'. If the layer is directional, node1id is the source (from) and node2id is the destination (to). The edge value 'value' is optional, defaulting to 1. If the specified node id's do not exist in the Nodeset, they are by default created and added, but by setting 'addmissingnodes' to 'false' prevents this.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1").ToLowerInvariant();
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg2");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg3");
            float value = command.GetArgumentParseFloat("value", 1);
            bool addMissingNodes = command.GetArgumentParseBool("addmissingnodes", true);
            ConsoleOutput.WriteLine(network.AddEdge(layerName, node1id, node2id, value, addMissingNodes).ToString());
        }
    }
}
