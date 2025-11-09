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
        public string Usage => "undefineattr(structure = [var:structure], attrname = [str])";
        public string Description => "Removes the definition of a node attribute for the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. This will also iterate through all nodes with attributes, removing the attributes for those that have it.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));

            //Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            ConsoleOutput.WriteLine(nodeset.UndefineNodeAttribute(attributeName).ToString());
        }
    }
}
