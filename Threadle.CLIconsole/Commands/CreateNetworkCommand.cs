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
    public class CreateNetworkCommand : ICommand
    {
        public string Usage => "[var:network] = network(*nodeset = [var:nodeset]|*createnodes = [int], *name = [str])";
        public string Description => "Creates a network with the name 'name' and assigning it to the variable [var:network]. Can optionally be provided with an existing Nodeset to use (as given by the variable [var:nodeset]): if not, a new Nodeset object will also be created and stored. For such a new Nodeset, it is possible to specify how many nodes that should be created with the 'createnodes' argument. Note that either 'nodeset' OR 'createnodes' can be provided, not both of these.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(true);
            string variableName = command.CheckAndGetAssignmentVariableName();
            Nodeset nodeset;
            if (command.GetArgument("nodeset") is string nameNodeset)
                nodeset = context.GetVariableThrowExceptionIfMissing<Nodeset>(nameNodeset);
            else
            {
                string variableNameNodeset = variableName + "_nodeset";
                int createnodes = command.GetArgumentParseInt("createnodes", 0);
                nodeset = new Nodeset(variableNameNodeset, createnodes);
                context.SetVariable(variableNameNodeset, nodeset);
                ConsoleOutput.WriteLine($"Nodeset '{nodeset.Name}' created and stored in variable '{variableNameNodeset}'");
            }

            string nameNetwork = command.GetArgumentParseString("name", variableName);
            //string nameNetwork = command.GetArgument("name") ?? context.GetNextIncrementalName("network-");
            Network network = new Network(nameNetwork, nodeset);
            context.SetVariable(variableName, network);
            ConsoleOutput.WriteLine($"Network '{nameNetwork}' created and stored in variable '{variableName}'");
        }
    }
}
