using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Parsing
{
    public interface ICommandParser
    {
        CommandPackage? Parse(string input);
    }
}
