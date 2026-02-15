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
    }
}
