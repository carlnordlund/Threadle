using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class ViewCommand : ICommand
    {
        public string Usage => "view(name = [var:structure])";
        public string Description => "View the content of the structure with the variable name [var:structure]. E.g. display(mynet) will display detailed information about the network with the variable 'mynet'.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            string structureName = command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0");
            if (!(context.GetVariable(structureName) is IStructure structure))
                ConsoleOutput.WriteLine($"Structure '{structureName}' not found.");
            else
                ConsoleOutput.WriteLine(structure.Content, true);
        }
    }
}
