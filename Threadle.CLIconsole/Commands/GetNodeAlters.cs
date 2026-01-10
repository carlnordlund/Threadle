using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getnodealters' CLI command.
    /// </summary>
    public class GetNodeAlters : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[array:uint] = getnodealters(network = [var:network], nodeId = [uint], *layernames = [semicolon-separated layer names], *direction = ['both'(default),'in','out'], *unique = [false(default),true])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Get the id of the alters to a specific node in the Network with the variable name [var:network]. Will either return the node alters for one or more specified layers (as given by layernames) or return alters in all layers. When specifying multiple layers, separate their names with a semicolon (;). Output is in standard JSON array format. By default, both in- and outbound ties are included in the set of alters, but this can be adjusted with the optional direction argument. When obtaining alters from multiple (or all) layers, the same alter node might appear in several layers. By default, such alter nodes will then appear multiple times in the returned array. However, by setting 'unique' to true, this array will be deduplicated before returned. (As a necessary side-effect of deduplication, the list will also be sorted, which it is not otherwise by design). Note: there is nothing stopping you from naming the same layer multiple times in the layernames string.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeId", "arg1");
            string layerNames = command.GetArgumentParseString("layernames", "");
            bool uniqueAlters = command.GetArgumentParseBool("unique", false);

            string[]? layers = (layerNames.Length > 0) ? layerNames.Split(';') : null;


            EdgeTraversal edgeTraversal = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.Both);
            //OperationResult<uint[]> result = network.GetNodeAlters(layerName, nodeId, edgeTraversal);
            OperationResult<uint[]> result = network.GetNodeAlters(layers, nodeId, edgeTraversal, uniqueAlters);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
