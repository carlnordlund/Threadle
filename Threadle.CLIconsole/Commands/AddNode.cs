using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Reflection.Metadata.Ecma335;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'addnode' CLI command.
    /// </summary>
    public class AddNode : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "addnode(structure = [var:structure], nodeid = [uint])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates and adds a node with id [nodeid] and adds it to the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. Note that the node id is what makes each node unique, and it must be an unsigned integer.";

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
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            return CommandResult.FromOperationResult(nodeset!.AddNode(nodeId));
        }
    }
}
