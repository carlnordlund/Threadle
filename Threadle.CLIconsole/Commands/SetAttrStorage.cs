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
        public string Syntax => "setattrstorage(structure = [var:structure], attrname = [str], storage = ['sparse','dense','auto'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Changes how the values of the node attribute 'attrname' are stored, in the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. The attribute keeps its name, type and all its values; only the storage changes. 'sparse' stores values per node, only for nodes that have the attribute set; 'dense' stores them in one flat column with an entry for every node. With 'auto', the storage estimated to use the least memory for this attribute is chosen, based on which nodes currently have it set and on whether those nodes also have other sparse attributes; as a rule of thumb, dense pays off when most nodes have the attribute. The estimates are included in the returned message. If the attribute already has the chosen storage, nothing changes.";

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
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            string storage = command.GetArgumentThrowExceptionIfMissingOrNull("storage", "arg2");
            return CommandResult.FromOperationResult(nodeset!.SetNodeAttributeStorage(attrName, storage));
        }
    }
}