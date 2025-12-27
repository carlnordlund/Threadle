using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    public interface ICommandParser
    {
        CommandPackage? Parse(string input);
    }
}
