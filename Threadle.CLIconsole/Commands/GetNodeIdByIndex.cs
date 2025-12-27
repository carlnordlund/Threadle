using Threadle.Core.Model;
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
    /// Class representing the 'getnodeidbyindex' CLI command.
    /// </summary>
    public class GetNodeIdByIndex : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[uint] = getnodeidbyindex(structure = [var:structure], index = [int])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Get the node id of the node with the specified index position in the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. Note that the index positions could change as nodes and node attributes are added and removed. Also note that nodes with attributes come first in the index, followed by nodes without attributes.";

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
            uint index = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("index", "arg1");
            if (!(nodeset!.GetNodeIdByIndex(index) is uint nodeId))
                return CommandResult.Fail("IndexOutOfRange", $"Node index '{index}' out of range");
            return CommandResult.Ok(
                message: $"Node id (index={index}): {nodeId}",
                payload: nodeId
                );
        }
    }
}
