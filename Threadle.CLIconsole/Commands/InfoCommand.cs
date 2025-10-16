using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    internal class InfoCommand : ICommand
    {
        public string Usage => "[str] = info(name = [var:structure])";
        
        public string Description => "Displays JSON-style metadata about the structure, which is either a Nodeset or a Network. Name and Filepath are included for both structures. Network structures also provide data on existing relational layers, their properties and number of edges. Nodeset structures also provide data on number of nodes, and existing node attributes and their types.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            IStructure structure = context.GetVariableThrowExceptionIfMissing<IStructure>(command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0"));
            ConsoleOutput.WriteLine(structure.Info);
        }

    }
}
