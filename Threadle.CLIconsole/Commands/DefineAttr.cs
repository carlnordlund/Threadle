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
    /// <summary>
    /// Note: I call this define and not add, because this is specifying a schema for node attributes, not actually adding anything to any nodes. Compare addlayer where a layer
    /// is actually added to a network.
    /// </summary>
    public class DefineAttr : ICommand
    {
        public string Usage => "defineattr(structure = [var:structure], attrname = [str], attrtype = ['int','char','float','bool'])";
        public string Description => "Defines a Node attribute for the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. The name of the node attribute is 'attrname' and its data type is one of 'int' (integer), 'char' (single character), 'float' (floating point), or 'bool' (boolean, true or false).";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));

            //Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            if (!command.TrimNameAndCheckValidity(attributeName, out string attributeNameVerified))
                throw new Exception($"!Error: Attribute name '{attributeName}' is not valid. It must start with a letter and contain only letters, digits, and underscores.");
            string attributeType = command.GetArgumentThrowExceptionIfMissingOrNull("attrtype", "arg2");
            ConsoleOutput.WriteLine(nodeset.DefineNodeAttribute(attributeName, attributeType).ToString());
        }
    }
}
