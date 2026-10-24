namespace Threadle.CLIconsole.Runtime
{
    /// <summary>
    /// Provides static settings that control the behavior of the command-line interface (CLI) at runtime.
    /// </summary>
    /// <remarks>This class exposes configuration options that influence how the CLI operates. The settings
    /// can be adjusted to modify aspects such as output verbosity or diagnostic detail during execution.</remarks>
    public static class CLISettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether verbose output is enabled.
        /// Note that this setting also affects the amount of information included in the JSON output when using JsonCommandResultRenderer, as it controls whether all details of the CommandResult are serialized or only a subset of key properties.
        /// </summary>
        /// <remarks>When set to <see langword="true"/>, detailed logging information is produced, which
        /// can be useful for debugging purposes. Setting this property to <see langword="false"/> reduces the amount of
        /// log output.</remarks>
        public static bool Verbose { get; set; } = true;

        /// <summary>
        /// Gets or sets whether each command's execution time (in milliseconds) is measured and
        /// appended, as a sentence, to the command's response Message (e.g. "Execution time:
        /// 12.34 ms."). Off by default, since most callers don't need per-command timing. Not a
        /// separate structured field - deliberately kept simple by riding along the existing
        /// Message text, which is already surfaced through the usual verbose/message pipeline on
        /// both the CLI's text-mode output and any client (e.g. threadleR's .th_call(), which
        /// already prints resp$Message when its own .print_message/verbose option is on) with no
        /// further wiring needed.
        /// </summary>
        public static bool ExecutionTime { get; set; } = false;
    }
}
