using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class GetWorkingDirectory : ICommand
    {
        public string Usage => "getwd()";

        public string Description => "Returns the current working directory that Threadle is currently using.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            var result = FileManager.GetCurrentDirectory();
            if (result.Success)
                ConsoleOutput.WriteLine(result.Value!, true);
            else
                ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
