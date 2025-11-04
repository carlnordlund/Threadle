using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class Setting : ICommand
    {
        public string Usage => "setting(name = [str], value = ['true','false'])";
        public string Description => "Changes the setting 'name' to either 'true' or 'false', i.e. either activating or deactivating it. Available settings are 'nodecache' (use node cache, lazy initialized), 'blockmultiedges' (prohibits the creation of multiple edges with identical connections and directions), 'onlyoutboundedges' (only stores outbound edges, i.e. no inbound edges, all to save memory for walker-only applications).";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            string param = command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0").ToLower();
            bool value = command.GetArgumentParseBoolThrowExceptionIfMissingOrNull("value", "arg1");
            if (param.Equals("verbose"))
                ConsoleOutput.Verbose = value;
            else if (param.Equals("endmarker"))
                ConsoleOutput.EndMarker = value;
            else
                ConsoleOutput.WriteLine(UserSettings.Set(param, value).ToString());
        }
    }
}
