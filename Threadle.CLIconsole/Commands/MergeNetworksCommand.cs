using PopnetEngine.CLIconsole.CLIUtilities;
using PopnetEngine.Core.Model;
using PopnetEngine.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.CLIconsole.Commands
{
    // I had this disabled for now. Better to first develop the Nodeset-related functions, like
    // filter, union, intersect, difference, and then have a function to create a subset of a network

    public class MergeNetworksCommand : ICommand
    {
        public string Usage => "[var:network] = merge(networks=[semicolon-separated list of var:network])";
        public string Description => "Takes all provided networks as given by the list of variables and merges them into a joint network object. All networks must use the same Nodeset object.";
        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(true);
            string assignedVariable = command.AssignedVariable!;
            string[] networkNames = command.GetArgumentThrowExceptionIfMissingOrNull("networks", "arg0").Split(';');
            if (networkNames.Length < 2)
                throw new Exception($"!Error: Must merge at least 2 networks.");

            List<Network> networks = new();
            Nodeset? nodeset = null;
            foreach (string networkName in networkNames)
            {
                Network network = context.GetVariable<Network>(networkName)
                    ?? throw new Exception($"!Error: No network with variable '{networkName}' found.");
                if (nodeset == null)
                    nodeset = network.Nodeset;
                else if (nodeset != network.Nodeset)
                    throw new Exception($"!Error: Nodeset of network '{networkName}' not the same as the nodeset of the first network.");
                networks.Add(network);
            }
            // Doesnt work right now

            Network mergedNetwork = Misc.MergeNetworks(networks);
            context.DeleteStructures(networks);
            context.SetVariable(assignedVariable, mergedNetwork);
            ConsoleOutput.WriteLine($"Merged individual networks {networkNames} into joint network and stored in variable '{assignedVariable}'. Note that layerId have changed!");

        }

    }
}
