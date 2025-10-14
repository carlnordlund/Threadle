using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class CreateNetworkCommand : ICommand
    {
        public string Usage => "[var:network] = network(*nodeset = [var:nodeset]|*capacity = [int], *name = [str])";
        public string Description => "Creates a network with the name 'name' and assigning it to the variable [var:network]. Can optionally be provided with an existing Nodeset to use (as given by the variable [var:nodeset]): if not, a new Nodeset object will also be created and stored. For such a new Nodeset, it is possible to prepare memory for its Node objects with the 'capacity' option: this will speed up the subsequent addition of new Node objects to this Nodeset. Note that either a Nodeset OR a capacity can be provided, not both of these.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(true);
            string nameAssigned = command.AssignedVariable!;
            Nodeset nodeset;
            if (command.GetArgument("nodeset") is string nameNodeset)
                nodeset = context.GetVariableThrowExceptionIfMissing<Nodeset>(nameNodeset);
            else
            {
                string name_nodeset_variable = nameAssigned + "_nodeset";
                int capacity = command.GetArgumentParseInt("capacity", 100);
                nodeset = new Nodeset(name_nodeset_variable, capacity);
                context.SetVariable(name_nodeset_variable, nodeset);
                ConsoleOutput.WriteLine($"Nodeset '{nodeset.Name}' created and stored in variable '{name_nodeset_variable}'");
            }

            string nameNetwork = command.GetArgument("name") ?? context.GetNextIncrementalName("network-");
            Network network = new Network(nameNetwork, nodeset);
            context.SetVariable(nameAssigned, network);
            ConsoleOutput.WriteLine($"Network '{network.Name}' created and stored in variable '{nameAssigned}'");
        }
    }
}
