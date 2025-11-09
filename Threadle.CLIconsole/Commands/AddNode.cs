using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Threadle.CLIconsole.Commands
{
    public class AddNode : ICommand
    {
        public string Usage => "addnode(structure = [var:structure], id = [uint])";

        public string Description => "Creates and adds a node with id [id] and adds it to the Nodeset that has the variable name [var:nodeset]. Note that the node id is what makes each node unique, and it must be an unsigned integer.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));

            //Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Core.Model.Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("id", "arg1");
            ConsoleOutput.WriteLine(nodeset.AddNode(nodeId).ToString());
        }
    }
}
