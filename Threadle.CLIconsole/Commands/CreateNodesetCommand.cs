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
    public class CreateNodesetCommand : ICommand
    {
        public string Usage => "[var:nodeset] = nodeset(*name = [str], *createnodes = [int (default:0)])";
        public string Description => "Creates an empty nodeset and stores it in the variable [var:nodeset]. An optional internal name 'name' can be provided. The Nodeset is by default empty, but nodes can also be created by specifying the number of nodes with the optional 'createnodes' integer value. The created nodes will then have id values starting from 0.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(true);
            string variableName = command.CheckAndGetAssignmentVariableName();

            string name = command.GetArgumentParseString("name", variableName);

            //string name = command.GetArgument("name") is string namestr ? namestr : command.AssignedVariable!;
            int createNodes = command.GetArgumentParseInt("createnodes", 0);
            if (createNodes < 0)
                throw new ArgumentException("Number of created nodes can not be less than zero");
            Nodeset nodeset = new Nodeset(name, createNodes);
            context.SetVariable(variableName, nodeset);
            ConsoleOutput.WriteLine($"Nodeset '{nodeset.Name}' created (with {createNodes} nodes) and stored in variable '{variableName}'");
        }
    }
}
