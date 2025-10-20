using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class GetNodeAltersCommand : ICommand
    {
        public string Usage => "[uint] = getnodealters(network = [var:structure], layername = [str], nodeid = [uint], *direction=['both'(default),'inbound','outbound'])";
        public string Description => "Get the id of the alters to a specific node in a specific layer, in standard JSON array format";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            uint nodeid = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg2");

            EdgeTraversal edgeTraversal = Misc.ParseEnumOrNull<EdgeTraversal>(command.GetArgument("direction"), EdgeTraversal.Both);

            OperationResult<uint[]> result = network.GetNodeAlters(layerName, nodeid, edgeTraversal);
            if (result.Success)
                ConsoleOutput.WriteLine("[" + string.Join(',', result.Value!) + "]", true);
            else
                ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
