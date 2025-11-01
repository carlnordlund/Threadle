using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class AddHyper : ICommand
    {
        public string Usage => "addhyper(network = [var:network], layername = [str], hypername = [str], *nodes = [semicolon-separated uints], *addmissingnodes = ['true'(default), 'false'])";

        public string Description => "Creates and adds a hyperedge (aka context or affiliation) called 'hypername' to layer 'layername' in the specified network. The layer must be 2-mode. This can then represent a particular school class or specific social event to which a set of nodes are affiliated. If there already is a hyperedge with the specified name, that is first removed. Can be provided with an optional list of nodes that are connected by this hyperedge. If the specified node id's do not exist in the Nodeset, they are by default created and added, but by setting 'addmissingnodes' to 'false' prevents this. Duplicate node id values are discarded.";


        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Core.Model.Network network = context.GetVariableThrowExceptionIfMissing<Core.Model.Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string hyperName = command.GetArgumentThrowExceptionIfMissingOrNull("hypername", "arg2");
            if (command.GetArgument("nodes") is string nodesString)
            {
                uint[] nodeIds = Misc.NodesIdsStringToArray(nodesString)
                    ?? throw new Exception($"!Error: Couldn't parse semicolon-separated list with nodes.");
                bool addMissingNodes = command.GetArgumentParseBool("addmissingnodes", true);
                ConsoleOutput.WriteLine(network.AddHyperedge(layerName, hyperName, nodeIds, addMissingNodes).ToString());
            }
            else
                ConsoleOutput.WriteLine(network.AddHyperedge(layerName, hyperName).ToString());
        }
    }
}
