using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class SetAttr : ICommand
    {

        public string Usage => "setattr(structure = [var:structure], nodeid = [uint], attrname = [str], attrvalue = [str])";
        public string Description => "Sets the value of the attribute 'attrname' to 'attrvalue' for the node with the id 'nodeid' in the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. Note that the node attribute must first have been defined, and that the value of the attribute must be in accordance with the specific data type for which it was defined.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));

            //Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg2");
            string attributeValue = command.GetArgumentThrowExceptionIfMissingOrNull("attrvalue", "arg3");
            ConsoleOutput.WriteLine(nodeset.SetNodeAttribute(nodeId, attributeName, attributeValue).ToString());
        }
    }
}
