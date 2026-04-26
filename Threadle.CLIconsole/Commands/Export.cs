using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using Threadle.Core.Utilities.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'export()' CLI command.
    /// </summary>
    public class Export : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "export(network = [var:network], format = ['gexf'], file = [str], +layername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Export a network info different external file formats. So far, only 'gexf' (Gephi format) is implemented, which still has to be specified with the 'format' argument. For gephi, the specific layer to export is specified with the 'layername' argument.";

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
            // This is implemented in preparation of future additional export formats, e.g. Pajek's net etc.
            // The format resolver is however in the FileManager.ExportNetworkToFile - here is just parsing
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            ExportFormat format = command.GetArgumentParseEnumThrowExceptionIfMissingOrNull<ExportFormat>("format", "arg1");
            string file = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg2");
            string layerName = command.GetArgumentParseString("layername", "");
            return CommandResult.FromOperationResult(FileManager.ExportNetworkToFile(network, format, layerName, file));
        }
    }
}
