using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class RemoveAttrCommand : ICommand
    {
        public string Usage => "removeattr(nodeset = [var:nodeset], nodeid = [uint], attrname = [str])";
        public string Description => "Remove the attribute 'attrname' from the node with the id 'nodeid' in the Nodeset with the variable [var:nodeset]. Note that the node attribute must first have been defined. If the attribute is defined but not set for this node, this will return success though noting that the attribute was not set for this node.";
        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg2");
            ConsoleOutput.WriteLine(nodeset.RemoveNodeAttribute(nodeId, attributeName).ToString());
        }
    }
}
