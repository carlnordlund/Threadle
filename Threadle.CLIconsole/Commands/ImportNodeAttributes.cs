using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    public class ImportNodeAttributes : ICommand
    {
        public string Syntax => "importnodeattributes(structure=[var:structure], file=\"[str]\", *addmissingnodes=['false'(default),'true'], *sep=[char(default:'\\t')])";

        public string Description => "Imports node attributes from a tab-separated file into the Nodeset associated with 'structure' (which can be a Nodeset or a Network). The file format mirrors a nodeset.tsv file: the first row is a header where the first cell is ignored and each subsequent cell defines an attribute name with an optional type annotation (e.g. 'age:Int', 'city:String'). Supported types are Int, Float, String, Bool, and Char; if no type annotation is given, String is assumed. Each subsequent row starts with a node id followed by the attribute values. By default, rows whose node id does not exist in the nodeset are skipped (left join). Set 'addmissingnodes=true' to add missing nodes instead.";

        public bool ToAssign => false;

        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetNodesetFromIStructure(context,
                command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"), out var nodeset)
                    is CommandResult commandResult)
                return commandResult;
            string filepath = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg1");
            bool addMissingNodes = command.GetArgumentParseBool("addmissingnodes", false);
            char separator = command.GetArgumentParseString("sep", "\t").FirstOrDefault('\t');
            return CommandResult.FromOperationResult(
                FileManager.ImportNodeAttributes(filepath, nodeset!, addMissingNodes, separator));
        }
    }
}