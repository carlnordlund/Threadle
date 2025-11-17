using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'createnetwork' CLI command.
    /// </summary>
    public class CreateNetwork : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = createnetwork(*nodeset = [var:nodeset]|*createnodes = [int], *name = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates a network with the name 'name' and assigning it to the variable [var:network]. Can optionally be provided with an existing Nodeset to use (as given by the variable [var:nodeset]): if not, a new Nodeset object will also be created and stored. For such a new Nodeset, it is possible to specify how many nodes that should be created with the 'createnodes' argument. Note that either 'nodeset' OR 'createnodes' can be provided, not both of these.";

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
            Nodeset nodeset;
            if (command.GetArgument("nodeset") is string nameNodeset)
                nodeset = context.GetVariableThrowExceptionIfMissing<Core.Model.Nodeset>(nameNodeset);
            else
            {
                string variableNameNodeset = variableName + "_nodeset";
                int createnodes = command.GetArgumentParseInt("createnodes", 0);
                nodeset = new Nodeset(variableNameNodeset, createnodes);
                context.SetVariable(variableNameNodeset, nodeset);
                ConsoleOutput.WriteLine($"Nodeset '{nodeset.Name}' created and stored in variable '{variableNameNodeset}'");
            }

            string nameNetwork = command.GetArgumentParseString("name", variableName);
            Network network = new Network(nameNetwork, nodeset);
            context.SetVariable(variableName, network);
            ConsoleOutput.WriteLine($"Network '{nameNetwork}' created and stored in variable '{variableName}'");
        }
    }
}
