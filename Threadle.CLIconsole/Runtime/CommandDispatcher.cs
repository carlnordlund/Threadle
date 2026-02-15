using System.Text.RegularExpressions;
using Threadle.CLIconsole.Commands;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;

namespace Threadle.CLIconsole.Runtime
{
    /// <summary>
    /// Provides a centralized mechanism for dispatching command-line interface (CLI) commands to their corresponding
    /// handlers within the application.
    /// </summary>
    /// <remarks>The CommandDispatcher class maintains a registry of available CLI commands and their
    /// associated ICommand implementations. It enables dynamic execution of commands based on user input, supports
    /// retrieval of command metadata for help and documentation purposes, and facilitates extensibility by allowing new
    /// commands to be added to the registry. This class is intended for internal use by the CLI infrastructure and is
    /// not intended to be instantiated.</remarks>
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
            ["exportlayer"] = new ExportLayer(),
            ["filter"] = new Filter(),
            ["generate"] = new Generate(),
            ["generateattr"] = new GenerateAttr(),
            ["getalledges"] = new GetAllEdges(),
            ["getallhyperedges"] = new GetAllHyperedges(),
            ["getallnodes"] = new GetAllNodes(),
            ["getattr"] = new GetAttr(),
            ["getattrs"] = new GetAttrs(),
            ["getattrsummary"] = new GetAttrSummary(),
            ["getedge"] = new GetEdge(),
            ["gethyperedgenodes"] = new GetHyperedgeNodes(),
            ["getnbrnodes"] = new GetNbrNodes(),
            ["getnodealters"] = new GetNodeAlters(),
            ["getnodehyperedges"] = new GetNodeHyperedges(),
            ["getnodeidbyindex"] = new GetNodeIdByIndex(),
            ["getrandomalter"] = new GetRandomAlter(),
            ["getrandomedge"] = new GetRandomEdge(),
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
        /// Returns the command class for a particular CLI command, or null if no such command exists.
        /// </summary>
        /// <param name="name">The CLI command for the particular <see cref="ICommand"/> class.</param>
        /// <returns></returns>
        internal static ICommand? GetCommand(string name)
            => _commands.TryGetValue(name.ToLower(), out var cmd) ? cmd : null;

        /// <summary>
        /// Retrieves help information for the specified command, including its syntax and description.
        /// </summary>
        /// <param name="commandName">The name of the command for which help information is requested. This parameter cannot be null or empty.</param>
        /// <returns>A CommandResult containing the help information for the specified command. If the command is not found, the
        /// result indicates failure with an appropriate error message.</returns>
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

        /// <summary>
        /// Retrieves a summary of all available commands, including their syntax and descriptions.
        /// </summary>
        /// <remarks>Use this method to display a comprehensive list of supported commands and their usage
        /// details to users. This can assist in building help menus or command reference documentation.</remarks>
        /// <returns>A CommandResult containing a collection of help information for each registered command. Each entry includes
        /// the command name, its syntax, and a description.</returns>
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

        /// <summary>
        /// Saves the syntax and descriptions of all registered commands to a specified file.
        /// </summary>
        /// <remarks>This method extracts the syntax and description for each registered command and
        /// writes them to the specified file in a tab-separated format. This method is specifically
        /// designed for updating the CLI command page on the THreadle website!</remarks>
        /// <param name="filepath">The path to the file where the command syntax details will be written. This value must not be null or empty.</param>
        /// <returns>A CommandResult indicating whether the operation succeeded. The result message contains the file path where
        /// the syntax details were saved.</returns>
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
