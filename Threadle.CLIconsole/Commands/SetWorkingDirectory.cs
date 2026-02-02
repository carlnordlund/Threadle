using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'setwd' CLI command.
    /// </summary>
    public class SetWorkingDirectory : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "setwd(dir = \"[str,'~','~documents','~examples']\")";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Sets the current working directory for Threadle to the relative or absolute path specified by 'dir'. There are three special options for this argument, options that work for all architectures. If set to '~', i.e. just the tilde character, the working directory will be set to the root user directory/folder. If set to '~documents', the working directory will be set to the user's documents folder (like the 'My Documents' folder in Windows). If a folder does not exist, an error is given.";

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
            string dir = command.GetArgumentThrowExceptionIfMissingOrNull("dir", "arg0");
            if (dir.Equals("~", StringComparison.OrdinalIgnoreCase))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            else if (dir.Equals("~documents", StringComparison.OrdinalIgnoreCase))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return CommandResult.FromOperationResult(FileManager.SafeSetCurrentDirectory(dir));
        }
    }
}
