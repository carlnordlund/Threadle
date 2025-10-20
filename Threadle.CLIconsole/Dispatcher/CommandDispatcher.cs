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
            _commands["clearlayer"] = new ClearLayerCommand();
            _commands["degree"]=new DegreeCommand();
            _commands["info"] = new InfoCommand();
            _commands["undefineattr"] = new UndefineAttrCommand();
            _commands["getnbrnodes"] = new GetNbrNodesCommand();
            _commands["getnodeidbyindex"] = new GetNodeIdByIndexCommand();
            _commands["getnodealters"] = new GetNodeAltersCommand();
            _commands["network"] = new CreateNetworkCommand();
            _commands["nodeset"] = new CreateNodesetCommand();
            _commands["delete"] = new RemoveCommand();
            _commands["deleteall"] = new RemoveAllCommand();
            _commands["addlayer"] = new AddLayerCommand();
            _commands["removelayer"] = new RemoveLayerCommand();
            _commands["addedge"] = new AddEdgeCommand();
            _commands["addhyper"] = new AddHyperCommand();
            //_commands["generate"] = new GenerateNetworkCommand();
            _commands["getedge"] = new GetEdgeCommand();
            _commands["checkedge"] = new CheckEdgeCommand();
            //_commands["info"] = new InfoCommand();
            _commands["view"]=new ViewCommand();
            _commands["addnode"] = new AddNodeCommand();
            _commands["defineattr"] = new DefineAttrCommand();
            _commands["setattr"] = new SetAttrCommand();
            _commands["getattr"] = new GetAttrCommand();
            _commands["removeattr"] = new RemoveAttrCommand();
            _commands["getrandom"] = new GetRandomNodeCommand();
            _commands["i"] = new InventoryCommand();
            _commands["setting"] = new SettingCommand();
            //_commands["setverbose"] = new SetVerboseCommand();
            _commands["density"] = new DensityCommand();
            _commands["getwd"] = new GetWorkingDirectoryCommand();
            _commands["setwd"] = new SetWorkingDirectoryCommand();
            _commands["loadfile"] = new LoadFileCommand();
            _commands["savefile"] = new SaveFileCommand();
            _commands["importlayer"] = new ImportLayerCommand();
            //_commands["import"] = new ImportCommand();
            //_commands["walker"] = new RandomWalkerCommand();
            //_commands["shortestpaths"] = new ShortestPathCommand();
            //_commands["mergenetworks"] = new MergeNetworksCommand();
            _commands["filter"] = new FilterCommand();

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
