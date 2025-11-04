using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class View : ICommand
    {
        public string Usage => "view(name = [var:structure])";
        public string Description => "View the content of the structure with the variable name [var:structure]. E.g. display(mynet) will display detailed information about the network with the variable 'mynet'.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            IStructure structure = context.GetVariableThrowExceptionIfMissing<IStructure>(command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0"));
            ConsoleOutput.WriteLine(structure.Content, true);
        }
    }
}
