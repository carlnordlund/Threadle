using Threadle.CLIconsole.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    public class CommandDispatcher
    {
        private readonly Dictionary<string, ICommand> _commands = new();

        public CommandDispatcher()
        {
            _commands["addedge"] = new AddEdge();
            _commands["addhyper"] = new AddHyper();
            _commands["addlayer"] = new AddLayer();
            _commands["addnode"] = new AddNode();
            _commands["checkedge"] = new CheckEdge();
            _commands["clearlayer"] = new ClearLayer();
            _commands["createnetwork"] = new CreateNetwork();
            _commands["createnodeset"] = new CreateNodeset();
            _commands["defineattr"] = new DefineAttr();
            _commands["degree"] = new Degree();
            _commands["delete"] = new Remove();
            _commands["deleteall"] = new RemoveAll();
            _commands["density"] = new Density();
            _commands["dichotomize"] = new Dichotomize();
            _commands["filter"] = new Filter();
            _commands["generate"] = new GenerateRandom();
            _commands["getattr"] = new GetAttr();
            _commands["getedge"] = new GetEdge();
            _commands["getnbrnodes"] = new GetNbrNodes();
            _commands["getnodealters"] = new GetNodeAlters();
            _commands["getnodeidbyindex"] = new GetNodeIdByIndex();
            _commands["getrandomalter"] = new GetRandomAlter();
            _commands["getrandomnode"] = new GetRandomNode();
            _commands["getwd"] = new GetWorkingDirectory();
            _commands["i"] = new Inventory();
            _commands["importlayer"] = new ImportLayer();
            _commands["info"] = new Info();
            _commands["loadfile"] = new LoadFile();
            _commands["removeattr"] = new RemoveAttr();
            _commands["removeedge"] = new RemoveEdge();
            _commands["removelayer"] = new RemoveLayer();
            _commands["savefile"] = new SaveFile();
            _commands["setattr"] = new SetAttr();
            _commands["setting"] = new Setting();
            _commands["setwd"] = new SetWorkingDirectory();
            _commands["undefineattr"] = new UndefineAttr();
            _commands["view"] = new View();
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
