using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'setattrstorage' CLI command.
    /// </summary>
    public class SetAttrStorage : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "setattrstorage(structure = [var:structure], storage = ['sparse','dense','auto'], *attrname = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Changes how node attribute values are stored, in the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. Attributes keep their names, types and all their values; only the storage changes. 'sparse' stores values per node, only for nodes that have the attribute set; 'dense' stores them in one flat column with an entry for every node. If 'attrname' is given, only that attribute is changed: with 'auto', it gets the storage estimated to use the least memory for it, based on how many nodes have it set (as a rule of thumb, dense pays off when more than about 80% of nodes have a value). If 'attrname' is omitted, all node attributes are changed: 'sparse' or 'dense' sets every attribute to that storage, and 'auto' chooses the combination of storages estimated to use the least memory for the nodeset as a whole, also returning the estimated memory before and after and which attributes ended up dense and sparse.";

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
            string storage = command.GetArgumentThrowExceptionIfMissingOrNull("storage", "arg1");
            string? attrName = command.GetArgument("attrname", "arg2");
            return CommandResult.FromOperationResult(nodeset!.SetNodeAttributeStorage(string.IsNullOrEmpty(attrName) ? null : attrName, storage));
        }
    }
}