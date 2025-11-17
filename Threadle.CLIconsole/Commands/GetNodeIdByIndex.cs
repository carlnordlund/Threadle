using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public string Description => "Get the node id of the node with the specified index position. Note that the index positions could change as nodes and node attributes are added and removed. Also note that nodes with attributes come first in the index, followed by nodes without attributes.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public void Execute(Command command, CommandContext context)
        {
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));
            uint index = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("index", "arg1");
            if (!(nodeset.GetNodeIdByIndex(index) is uint nodeId))
                throw new Exception($"Node index '{index}' out of range");
            ConsoleOutput.WriteLine(nodeId.ToString(), true);
        }
    }
}
