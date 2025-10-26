using Threadle.CLIconsole.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;

namespace Threadle.CLIconsole.Dispatcher
{
    public class CommandDispatcher
    {
        private readonly Dictionary<string, ICommand> _commands = new();

        public CommandDispatcher()
        {
            _commands["dichotomize"] = new Dichotomize();
            _commands["getrandomalter"] = new GetRandomAlter();
            _commands["clearlayer"] = new ClearLayer();
            _commands["degree"]=new Degree();
            _commands["info"] = new Info();
            _commands["undefineattr"] = new UndefineAttr();
            _commands["getnbrnodes"] = new GetNbrNodes();
            _commands["getnodeidbyindex"] = new GetNodeIdByIndex();
            _commands["getnodealters"] = new GetNodeAlters();
            _commands["createnetwork"] = new CreateNetwork();
            _commands["createnodeset"] = new CreateNodeset();
            _commands["delete"] = new Remove();
            _commands["deleteall"] = new RemoveAll();
            _commands["addlayer"] = new AddLayer();
            _commands["removelayer"] = new RemoveLayer();
            _commands["addedge"] = new AddEdge();
            _commands["addhyper"] = new AddHyper();
            //_commands["generate"] = new GenerateNetworkCommand();
            _commands["getedge"] = new GetEdge();
            _commands["checkedge"] = new CheckEdge();
            //_commands["info"] = new InfoCommand();
            _commands["view"]=new View();
            _commands["addnode"] = new AddNode();
            _commands["defineattr"] = new DefineAttr();
            _commands["setattr"] = new SetAttr();
            _commands["getattr"] = new GetAttr();
            _commands["removeattr"] = new RemoveAttr();
            _commands["getrandomnode"] = new GetRandomNode();
            _commands["i"] = new Inventory();
            _commands["setting"] = new Setting();
            //_commands["setverbose"] = new SetVerboseCommand();
            _commands["density"] = new Density();
            _commands["getwd"] = new GetWorkingDirectory();
            _commands["setwd"] = new SetWorkingDirectory();
            _commands["loadfile"] = new LoadFile();
            _commands["savefile"] = new SaveFile();
            _commands["importlayer"] = new ImportLayer();
            //_commands["import"] = new ImportCommand();
            //_commands["walker"] = new RandomWalkerCommand();
            //_commands["shortestpaths"] = new ShortestPathCommand();
            //_commands["mergenetworks"] = new MergeNetworksCommand();
            _commands["filter"] = new Filter();

        }

        public void Dispatch(Command command, CommandContext context)
        {
            if (_commands.TryGetValue(command.CommandName.ToLower(), out var handler))
                handler.Execute(command, context);
            else
                ConsoleOutput.WriteLine($"!Error: Unknown command: {command.CommandName}");
        }

        public Dictionary<string, ICommand> GetAllCommands() => _commands;

        public ICommand? GetCommand(string name)
            => _commands.TryGetValue(name.ToLower(), out var cmd) ? cmd : null;
    }
}
