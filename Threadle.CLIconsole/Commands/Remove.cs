using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class Remove : ICommand
    {
        public string Usage => "remove(name = [var:structure])";

        public string Description => "Removes (deletes) the structure with the variable name 'name'. A Nodeset can not be deleted if it is currently used by another structure: first delete those structures. If deleting a network, the nodeset that it uses will remain.";

        public bool ToAssign => false;
        public void Execute(Command command, CommandContext context)
        {
            string structureName = command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0");
            ConsoleOutput.WriteLine(context.RemoveStructure(structureName).ToString());
        }
    }
}
