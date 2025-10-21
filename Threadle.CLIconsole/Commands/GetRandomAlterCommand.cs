using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    public class GetRandomAlterCommand : ICommand
    {
        public string Usage => "[uint] = getrandomalter(network = [var:network], nodeid = [uint], *layername = [str], *direction = ['both'(default),'in','out'], *balanced = ['true','false'(default)])";
        public string Description => "Get the node id of a random alter to the specified node. By default, the pick is randomly picked among all available layers, or the specified layer. By default, both in- and outbound ties are considered, but this can be adjusted.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            uint nodeid = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string layerName = command.GetArgumentParseString("layername", "");
            EdgeTraversal edgeTraversal = Misc.ParseEnumOrNull<EdgeTraversal>(command.GetArgument("direction"), EdgeTraversal.Both);
            bool balance = command.GetArgumentParseBool("balanced", false);

            OperationResult<uint> result = Analyses.GetRandomAlter(network, nodeid, layerName, edgeTraversal, balance);
            if (result.Success)
                ConsoleOutput.WriteLine(result.Value.ToString(), true);
            else
                ConsoleOutput.WriteLine(result.ToString());


            //string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");

            //EdgeTraversal edgeTraversal = Misc.ParseEnumOrNull<EdgeTraversal>(command.GetArgument("direction"), EdgeTraversal.Both);

            //OperationResult<uint[]> result = network.GetNodeAlters(layerName, nodeid, edgeTraversal);
            //if (result.Success)
            //    ConsoleOutput.WriteLine("[" + string.Join(',', result.Value!) + "]", true);
            //else
            //    ConsoleOutput.WriteLine(result.ToString());
        }

    }
}
