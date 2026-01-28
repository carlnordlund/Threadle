using Threadle.CLIconsole.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using System.Text.RegularExpressions;

namespace Threadle.CLIconsole.Runtime
{
    public static class CommandDispatcher
    {
        #region Fields
        /// <summary>
        /// Stores all available CLI commands and their corresponding classes
        /// </summary>
        private static readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>
        {
            ["addaff"] = new AddAffiliation(),
            ["addaff"] = new AddAffiliation(),
            ["addedge"] = new AddEdge(),
            ["addhyper"] = new AddHyper(),
            ["addlayer"] = new AddLayer(),
            ["addnode"] = new AddNode(),
            ["checkedge"] = new CheckEdge(),
            ["clearlayer"] = new ClearLayer(),
            ["components"] = new Components(),
            ["createnetwork"] = new CreateNetwork(),
            ["createnodeset"] = new CreateNodeset(),
            ["defineattr"] = new DefineAttr(),
            ["degree"] = new Degree(),
            ["delete"] = new Delete(),
            ["deleteall"] = new DeleteAll(),
            ["density"] = new Density(),
            ["dichotomize"] = new Dichotomize(),
            ["dir"] = new Dir(),
            ["filter"] = new Filter(),
            ["generate"] = new Generate(),
            ["generateattr"] = new GenerateAttr(),
            ["getalledges"] = new GetAllEdges(),
            ["getallhyperedges"] = new GetAllHyperedges(),
            ["getattr"] = new GetAttr(),
            ["getattrsummary"] = new GetAttrSummary(),
            ["getedge"] = new GetEdge(),
            ["gethyperedgenodes"] = new GetHyperedgeNodes(),
            ["getnbrnodes"] = new GetNbrNodes(),
            ["getnodealters"] = new GetNodeAlters(),
            ["getnodehyperedges"] = new GetNodeHyperedges(),
            ["getnodeidbyindex"] = new GetNodeIdByIndex(),
            ["getrandomalter"] = new GetRandomAlter(),
            ["getrandomnode"] = new GetRandomNode(),
            ["getwd"] = new GetWorkingDirectory(),
            ["help"] = new HelpCommand(),
            ["i"] = new Inventory(),
            ["importlayer"] = new ImportLayer(),
            ["info"] = new Info(),
            ["loadscript"] = new LoadScript(),
            ["loadfile"] = new LoadFile(),
            ["preview"] = new Preview(),
            ["randomseed"] = new RandomSeed(),
            ["removeaff"] = new RemoveAffiliation(),
            ["removeattr"] = new RemoveAttr(),
            ["removeedge"] = new RemoveEdge(),
            ["removehyper"] = new RemoveHyper(),
            ["removelayer"] = new RemoveLayer(),
            ["removenode"] = new RemoveNode(),
            ["savefile"] = new SaveFile(),
            ["setattr"] = new SetAttr(),
            ["setting"] = new Setting(),
            ["setwd"] = new SetWorkingDirectory(),
            ["shortestpath"] = new ShortestPath(),
            ["subnet"] = new Subnet(),
            ["symmetrize"] = new Symmetrize(),
            ["undefineattr"] = new UndefineAttr()
        };
        #endregion


        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandDispatcher"/> class, setting
        /// all available CLI commands and their corresponding classes.
        /// </summary>
        //public CommandDispatcher()
        //{
        //    _commands["addaff"] = new AddAffiliation();
        //    _commands["addedge"] = new AddEdge();
        //    _commands["addhyper"] = new AddHyper();
        //    _commands["addlayer"] = new AddLayer();
        //    _commands["addnode"] = new _addNodeWithAttributes();
        //    _commands["checkedge"] = new CheckEdge();
        //    _commands["clearlayer"] = new ClearLayer();
        //    _commands["createnetwork"] = new CreateNetwork();
        //    _commands["createnodeset"] = new CreateNodeset();
        //    _commands["defineattr"] = new DefineAttr();
        //    _commands["degree"] = new Degree();
        //    _commands["delete"] = new Remove();
        //    _commands["deleteall"] = new RemoveAll();
        //    _commands["density"] = new Density();
        //    _commands["dichotomize"] = new Dichotomize();
        //    _commands["filter"] = new Filter();
        //    _commands["generate"] = new GenerateRandom();
        //    _commands["getattr"] = new GetAttr();
        //    _commands["getedge"] = new GetEdge();
        //    _commands["getnbrnodes"] = new GetNbrNodes();
        //    _commands["getnodealters"] = new GetNodeAlters();
        //    _commands["getnodeidbyindex"] = new GetNodeIdByIndex();
        //    _commands["getrandomalter"] = new GetRandomAlter();
        //    _commands["getrandomnode"] = new GetRandomNode();
        //    _commands["getwd"] = new GetWorkingDirectory();
        //    _commands["i"] = new Inventory();
        //    _commands["importlayer"] = new ImportLayer();
        //    _commands["info"] = new Info();
        //    _commands["loadfile"] = new LoadFile();
        //    _commands["preview"] = new Preview();
        //    _commands["removeaff"] = new RemoveAffiliation();
        //    _commands["removeattr"] = new RemoveAttr();
        //    _commands["removeedge"] = new RemoveEdge();
        //    _commands["removehyper"] = new RemoveHyper();
        //    _commands["removelayer"] = new RemoveLayer();
        //    _commands["removenode"] = new RemoveNode();
        //    _commands["savefile"] = new SaveFile();
        //    _commands["setattr"] = new SetAttr();
        //    _commands["setting"] = new Setting();
        //    _commands["setwd"] = new SetWorkingDirectory();
        //    _commands["subnet"] = new Subnet();
        //    _commands["undefineattr"] = new UndefineAttr();
        //}
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Method to dispatch a particular command, providing the current command context (i.e.
        /// the console variable memory).
        /// </summary>
        /// <param name="command">The <see cref="CommandPackage"/> object describing a particular CLI command with argument values and possible assignment.</param>
        /// <param name="context">The <see cref="CommandContext"/> object holding the current console variable memory.</param>
        /// <returns>A <see cref="CommandResult"/> object informing how the command went, including possible payload</returns>
        internal static CommandResult Dispatch(CommandPackage command, CommandContext context)
        {
            if (_commands.TryGetValue(command.CommandName.ToLower(), out var handler))
            {
                command.CheckAssigment(handler.ToAssign);
                return handler.Execute(command, context);
            }
            return CommandResult.Fail(
                code: "UnknownCommand",
                message: $"Unknown command: {command.CommandName}"
                );
        }

        /// <summary>
        /// Returns the dictionary of all commands, used by the command loop when responding to the
        /// 'help' CLI command.
        /// </summary>
        /// <returns>Returns the dictionary of CLI commands and their corresponding classes.</returns>
        internal static Dictionary<string, ICommand> GetAllCommands() => _commands;

        /// <summary>
        /// Returns the command class for a particular CLI command, or null if no such command exists.
        /// </summary>
        /// <param name="name">The CLI command for the particular <see cref="ICommand"/> class.</param>
        /// <returns></returns>
        internal static ICommand? GetCommand(string name)
            => _commands.TryGetValue(name.ToLower(), out var cmd) ? cmd : null;

        internal static CommandResult GetHelpFor(string commandName)
        {
            if (!(GetCommand(commandName) is ICommand command))
                return CommandResult.Fail("CommandNotFound", $"The command '{commandName}' was not found.");


            Dictionary<string, string> helpText = new()
            {
                ["Command"] = commandName,
                ["Syntax"] = command.Syntax,
                ["Description"] = command.Description
            };
            return CommandResult.Ok(
                $"Help for command '{commandName}':",
                helpText
                );

        }

        internal static CommandResult GetHelpForAll()
        {
            List<Dictionary<string, string>> helpTexts = [];
            foreach (var kvp in _commands)
                helpTexts.Add(new Dictionary<string, string>()
                {
                    ["Command"] = kvp.Key,
                    ["Syntax"] = kvp.Value.Syntax,
                    ["Description"] = kvp.Value.Description
                });

            return CommandResult.Ok(
                "Available commands",
                helpTexts
            );
        }

        internal static CommandResult DumpHelpToFile(string filepath)
        {
            var pattern = @"^(?:\s*(\[[^\]]+\])\s*=\s*)?([a-zA-Z_]\w*)\s*\(\s*(.*?)\s*\)\s*$";
            List<string> lines = [];
            foreach (var kvp in _commands)
            {
                var match = Regex.Match(kvp.Value.Syntax, pattern);
                lines.Add(match.Groups[1].Value + "\t" + match.Groups[2].Value + "\t" + match.Groups[3].Value + "\t" + kvp.Value.Description);
            }
            File.WriteAllLines(filepath, lines.ToArray());

            return CommandResult.Ok($"Saved syntax details to file '{filepath}'");
        }


        #endregion
    }
}
