using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'defineattr' CLI command.
    /// </summary>
    public class DefineAttr : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "defineattr(structure = [var:structure], attrname = [str], attrtype = ['int','char','float','bool'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Defines a Node attribute for the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. The name of the node attribute is 'attrname' and its data type is one of 'int' (integer), 'char' (single character), 'float' (floating point), or 'bool' (boolean, true or false).";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetNodesetFromIStructure(context, command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;
            //Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            if (!command.TrimNameAndCheckValidity(attributeName, out string attributeNameVerified))
                return CommandResult.Fail("AttributeNameFormatError", $"Error: Attribute name '{attributeName}' is not valid. It must start with a letter and contain only letters, digits, and underscores.");
            string attributeType = command.GetArgumentThrowExceptionIfMissingOrNull("attrtype", "arg2");
            return CommandResult.FromOperationResult(nodeset!.DefineNodeAttribute(attributeName, attributeType));
            //ConsoleOutput.WriteLines(nodeset.DefineNodeAttribute(attributeName, attributeType).ToString());
        }
    }
}
