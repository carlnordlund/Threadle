using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class GetNodeIdByIndex : ICommand
    {
        public string Usage => "[uint] = getnodeidbyindex(structure = [var:structure], index = [int])";
        public string Description => "Get the node id of the node with the specified index position. Note that the index positions could change as nodes and node attributes are added and removed. Also note that nodes with attributes come first in the index, followed by nodes without attributes.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));
            uint index = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("index", "arg1");
            if (!(nodeset.GetNodeIdByIndex(index) is uint nodeId))
                throw new Exception($"Node index '{index}' out of range");
            ConsoleOutput.WriteLine(nodeId.ToString(), true);
        }
    }
}
