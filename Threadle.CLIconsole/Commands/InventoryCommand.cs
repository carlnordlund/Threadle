using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class InventoryCommand : ICommand
    {
        public string Usage => "i()";
        public string Description => "Provides an inventory of the currently stored data objects. Note that the brackets can be ignored. (This command is in honor of all 1970's text adventure games, where 'i' was used to check what you were carrying).";


        public void Execute(Command command, CommandContext context)
        {
            if (context.Variables.Any())
            {
                ConsoleOutput.WriteLine("Stored objects:");
                foreach ((string name, IStructure structure) in context.Variables)
                    ConsoleOutput.WriteLine($"{name} [{structure.GetType().Name}]" + (structure.Filepath.Length > 0 ? ": " + structure.Filepath : "") + (structure.IsModified ? " (Modified)" : ""), true);
            }
            else
                ConsoleOutput.WriteLine("No objects stored.");
        }
    }
}
