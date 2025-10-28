using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class GetAttr : ICommand
    {
        public string Usage => "[str] = getattr(nodeset = [var:nodeset], nodeid = [uint], attrname = [str])";
        public string Description => "Gets the value of the attribute 'attrname' for node 'nodeid' in the Nodeset with the variable [var:nodeset]. Note that the node attribute must first have been defined. Returns an empty string if the node has no value for this attribute.";
        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Core.Model.Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg2");
            OperationResult<NodeAttributeValue> result = nodeset.GetNodeAttribute(nodeId, attributeName);
            if (result.Success)
                ConsoleOutput.WriteLine(result.Value.ToString(), true);
            else
                ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
