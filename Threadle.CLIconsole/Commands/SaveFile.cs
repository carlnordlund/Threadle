using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'savefile' CLI command.
    /// </summary>
    public class SaveFile : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "savefile(structure = [var:structure], *file = \"[str]\")";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Saves the structure [var:structure] to file. By default, the Filepath of the structure is used to save to, but if the 'file' argument is provided, it will instead be saved to this, which will replace Filepath. If the Filepath property is not set (i.e. it hasn't been saved before, or loaded from file) and no 'file' is provided, an error will be returned. If the structure is a Network, this will also save the Nodeset to its file if it has been modified. However, if the Nodeset has not been saved before (or loaded from file), i.e. it has a null Filepath property, that will result in an error: you must then first save the Nodeset as a file before saving the Network. If the filename ends with .tsv, the structure is saved in tab-separated value format. With .tsv.gz, it is saved in tsv with additional Gzipped. If the file ending is .bin, the structure is saved in Threadle's own binary format, which is a compact, non-human-readable format. If the file ending is .bin.gz, it is the Gzipped version of the .bin format, which is typically quite compact and small.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public void Execute(Command command, CommandContext context)
        {
            IStructure structure = context.GetVariableThrowExceptionIfMissing<IStructure>(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));
            string filepath = command.GetArgumentParseString("file", structure.Filepath);            

            OperationResult result = FileManager.Save(structure, filepath);
            ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
