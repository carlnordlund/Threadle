using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class SetWorkingDirectory : ICommand
    {
        public string Usage => "setwd(dir = [str])";

        public string Description => "Sets the current working directory for Threadle to 'dir'.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            string dir = command.GetArgumentThrowExceptionIfMissingOrNull("dir", "arg0");
            string newDir = FileManager.SafeSetCurrentDirectory(dir);
            ConsoleOutput.WriteLine("Current working directory:");
            ConsoleOutput.WriteLine(newDir);
        }
    }
}
