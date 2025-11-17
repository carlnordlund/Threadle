using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'createnodeset' CLI command.
    /// </summary>
    public class CreateNodeset : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:nodeset] = createnodeset(*name = [str], *createnodes = [int(default:0)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates an empty nodeset and stores it in the variable [var:nodeset]. An optional internal name 'name' can be provided. The Nodeset is by default empty, but nodes can also be created by specifying the number of nodes with the optional 'createnodes' integer value. The created nodes will then have id values starting from 0.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => true;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public void Execute(Command command, CommandContext context)
        {
            string variableName = command.CheckAndGetAssignmentVariableName();
            string name = command.GetArgumentParseString("name", variableName);
            int createNodes = command.GetArgumentParseInt("createnodes", 0);
            if (createNodes < 0)
                throw new ArgumentException("Number of created nodes can not be less than zero");
            Nodeset nodeset = new Core.Model.Nodeset(name, createNodes);
            context.SetVariable(variableName, nodeset);
            ConsoleOutput.WriteLine($"Nodeset '{nodeset.Name}' created (with {createNodes} nodes) and stored in variable '{variableName}'");
        }
    }
}
