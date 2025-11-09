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
    public class RemoveNode : ICommand
    {
        public string Usage => "removenode(structure = [var:structure], nodeid = [uint])";

        public string Description => "Removes the specified node from the the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. This CLI command will also iterate through all stored Network structures, removing related edges for the networks that use this Nodeset.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));
            //Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            OperationResult result = nodeset.RemoveNode(nodeId);
            ConsoleOutput.WriteLine(result.ToString());
            if (!result.Success)
                return;
            foreach (Network network in context.GetNetworksUsingNodeset(nodeset))
            {
                network.RemoveNodeEdges(nodeId);
                ConsoleOutput.WriteLine($"Also removing edges involving node '{nodeId}' from network '{network.Name}'.");
            }
        }
    }
}
