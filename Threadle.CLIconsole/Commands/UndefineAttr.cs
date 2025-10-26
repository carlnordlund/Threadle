using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    public class UndefineAttr : ICommand
    {
        public string Usage => "undefineattr(nodeset = [var:nodeset], attrname = [str])";
        public string Description => "Removes the definition of a node attribute for the specified nodeset. This will also iterate through all nodes with attributes, removing the attributes for those that have it.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Core.Model.Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Core.Model.Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");


            ConsoleOutput.WriteLine(nodeset.UndefineNodeAttribute(attributeName).ToString());
        }

    }
}
