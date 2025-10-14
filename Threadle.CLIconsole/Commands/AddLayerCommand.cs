using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class AddLayerCommand : ICommand
    {
        public string Usage => "addlayer(network = [var:network], layername = [str], mode = ['1', '2'], *directed = ['true', 'false'(default)], *valuetype = ['binary'(default), 'valued'], *selfties=['true', 'false'(default)])";

        public string Description => "Adds a relational layer for a network, which can be either 1-mode or 2-mode (with hyperedges). For 1-mode layers, there are additional settings for the type of ties that exist, specifying edge directionality, edge value type and whether selfties are allowed or not. For 2-mode layer, these settings do not matter: all ties are deemed binary. A network must have at least one layer defined, referred to when adding edges between nodes and nodes-affiliations as well as when importing edges from file to a layer.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            char mode = command.GetArgumentThrowExceptionIfMissingOrNull("mode", "arg2")[0];
            OperationResult result;

            if (mode == '1')
            {
                result = network.AddLayerOneMode(
                    layerName: layerName,
                    edgeDirectionality: command.GetArgumentParseBool("directed", false) ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected,
                    edgeValueType: Misc.ParseEnumOrNull<EdgeType>(command.GetArgument("valuetype"), EdgeType.Binary),
            selfties: command.GetArgumentParseBool("selfties", false));
            }
            else if (mode == '2')
            {
                result = network.AddLayerTwoMode(layerName);
            }
            else
                throw new Exception($"!Error: Unknown mode ('{mode}') - must be either '1' or '2'.");
            ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
