using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'setattrs' CLI command.
    /// </summary>
    public class SetAttrs : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "setattrs(structure = [var:structure], *nodes = [semicolon-separated uints], attrname = [str], +attrvalue = [str], +attrvalues = [semicolon-separated strs])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Sets the value of the attribute 'attrname' for multiple nodes in the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. The nodes are given as a semicolon-separated list in 'nodes': if 'nodes' is omitted, the attribute is set for all nodes in the Nodeset. Exactly one of 'attrvalue' and 'attrvalues' must be provided: 'attrvalue' sets the same value for all nodes, whereas 'attrvalues' is a semicolon-separated list with one value per node, in the same order as 'nodes'. Note that the node attribute must first have been defined, and that the values must be in accordance with the specific data type for which it was defined. All nodes and values are validated before any attribute is set: if any node is missing or any value can not be parsed, nothing is changed.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console variable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetNodesetFromIStructure(context, command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname");

            uint[]? nodeIds = null;
            string nodesString = command.GetArgumentParseString("nodes", "");
            if (nodesString.Length > 0 && (nodeIds = Misc.SplitStringToUintArray(nodesString)) == null)
                return CommandResult.Fail("ParseError", "Could not parse semicolon-separated list with nodes.");

            string attrValue = command.GetArgumentParseString("attrvalue", "");
            string attrValues = command.GetArgumentParseString("attrvalues", "");
            if ((attrValue.Length > 0) == (attrValues.Length > 0))
                return CommandResult.Fail("InvalidArguments", "Exactly one of 'attrvalue' and 'attrvalues' must be provided.");
            if (attrValues.Length > 0 && nodeIds == null)
                return CommandResult.Fail("InvalidArguments", "'attrvalues' requires that 'nodes' is provided.");
            string[] values = attrValue.Length > 0 ? [attrValue] : attrValues.Split(';');

            return CommandResult.FromOperationResult(nodeset!.SetMultipleNodeAttributes(nodeIds, attrName, values));
        }
    }
}
