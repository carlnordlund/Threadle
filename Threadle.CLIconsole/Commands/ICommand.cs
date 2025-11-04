using Threadle.CLIconsole.CLIUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public interface ICommand
    {
        string Usage { get; }
        string Description { get; }
        bool ToAssign { get; }
        void Execute(Command parsedCommand, CommandContext context);
    }
}
