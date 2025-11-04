using Threadle.CLIconsole.CLIUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class RemoveAll : ICommand
    {
        public string Usage => "removeall()"; 
        
        public string Description => "Removes (deletes) all current structures stored in the variable space. Use with caution!";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            context.RemoveAllStructures();
            ConsoleOutput.WriteLine($"Deleted all structures.");
        }
    }
}
