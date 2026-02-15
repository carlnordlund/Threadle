using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getattr' CLI command.
    /// </summary>
    public class GetAttrs : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[array:str] = getattrs(structure = [var:structure], nodes = [semicolon-separated uints], attrname = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Gets the values of the attribute 'attrname' for the node ids that is provided in the semi-colon separated list. Returns a string-object dictionary. If an attribute lacks this attribute, a null value is returned.";

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
            string nodesString = command.GetArgumentThrowExceptionIfMissingOrNull("nodes", "arg1");
            if (Misc.SplitStringToUintArray(nodesString) is not uint[] nodeIds || nodeIds.Length == 0)
                return CommandResult.Fail("ParseError", "Could not parse semicolon-separated list with nodes.");
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg2");

            OperationResult<Dictionary<uint, object?>> result = nodeset!.GetMultipleNodeAttributes(nodeIds, attrName);
            if (!result.Success)
                return CommandResult.Fail(result.Code, result.Message);
            return CommandResult.Ok(result.Message, result.Value);
        }
    }
}
