using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class GetAllNodes : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "getallnodes(structure = [var:structure], *offset = [int(default:0)], *limit = [int(default:1000])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Returns a JSON-ready vector of the node ids of all nodes in the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. Will return maximum 1000 by default, but this can be changed with the 'limit' argument. Starts with the first node (index zero) by default, but this can be adjusted with 'offset'. Thus, if there are more than 1000 edges in the layer, all can be obtained either by using 'offset' for pagination and/or increasing the 'limit'. Do note that the node order reflects the order in which they were added, which thus may or may not be the same as their node ids.";

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
            if (CommandHelpers.TryGetNodesetFromIStructure(context, command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;
            int offset = command.GetArgumentParseInt("offset", 0);
            int limit = command.GetArgumentParseInt("limit", 1000);

            OperationResult<List<uint>> result = nodeset!.GetAllNodes(offset, limit);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
