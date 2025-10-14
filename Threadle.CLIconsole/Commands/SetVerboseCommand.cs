using PopnetEngine.CLIconsole.CLIUtilities;
using PopnetEngine.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.CLIconsole.Commands
{
    public class SetVerboseCommand : ICommand
    {
        public string Usage => "setverbose(verbose=['true','false'])";
        public string Description => "Sets the verbose state to either on (true) or off (false). If verbose is turned off (false), the CLI client will only display results from commands such as getattr(), density(), getrandom(). If verbose is turned on (true), which is the default, the CLI client will be more verbose. If the client's exe file is started with the '-p' argument, this is equal to setting verbose level to false from the very start. Note: the help() command will override this setting and always output the help info.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            bool verbose = command.GetArgumentParseBoolThrowExceptionIfMissingOrNull("verbose", "arg0");
            ConsoleOutput.Verbose = verbose;
            ConsoleOutput.WriteLine($"Verbose set to '{verbose}'.");
        }
    }
}
