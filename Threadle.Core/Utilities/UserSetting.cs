namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Static class holding various system settings that affects how Threadle works and
    /// stores data.
    /// </summary>
    public static class UserSettings
    {
        #region Properties
        /// <summary>
        /// Setting for whether a NodeCache should be kept active (true) or only generated on-the-fly when needed (false).
        /// An active NodeCache makes it slightly faster to iterate a nodeset, at the expense of storing all node ids in a separate array.
        /// </summary>
        public static bool NodeCache { get; set; } = true;

        /// <summary>
        /// Setting for whether the adding of multiedges should be checked and blocked (true) or not (false).
        /// When active, it is not possible to add a new edge between two nodes if there is already one there.
        /// </summary>
        public static bool BlockMultiedges { get; set; } = true;

        /// <summary>
        /// Setting for the maximum number of threads that parallelized methods (e.g. betweenness centrality)
        /// are allowed to use concurrently (the 'maxthreads' setting). -1 (the default) means unconstrained,
        /// i.e. .NET decides based on the number of available cores. Any other value must be a positive
        /// integer no greater than Environment.ProcessorCount, nor greater than <see cref="MaxThreadsLimit"/>
        /// if that is set. Its value at startup can be set with 'defaultmaxthreads' in appconfig.json.
        /// </summary>
        public static int MaxDegreeOfParallelism { get; set; } = -1;

        /// <summary>
        /// Limit for the 'maxthreads' setting, set by 'maxthreadslimit' in appconfig.json (see <see cref="AppConfig"/>).
        /// -1 (the default) means no limit other than the number of available processor cores.
        /// When a limit is set, 'maxthreads' cannot be set above it, and setting 'maxthreads' to -1 uses the limit.
        /// </summary>
        public static int MaxThreadsLimit { get; set; } = -1;
        #endregion


        #region Methods
        /// <summary>
        /// Method for modifying the value of a setting. Each known setting name is hardcoded here, together
        /// with the type it expects and any extra validation; 'value' is parsed/converted to that type,
        /// whether it arrives already typed (e.g. from other C# code) or as a string (e.g. from the CLI).
        /// </summary>
        /// <param name="key">The name of the setting (in lowercase).</param>
        /// <param name="value">The new value of the setting, either already typed (bool/int) or as a string to be parsed.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how this went.</returns>
        public static OperationResult Set(string key, object value)
        {
            switch (key)
            {
                case "nodecache":
                    if (!Misc.TryConvertToBool(value, out bool nodeCacheValue))
                        return OperationResult.Fail("InvalidValue", $"Setting 'nodecache' requires a boolean value ('true'/'false'), got '{value}'.");
                    NodeCache = nodeCacheValue;
                    break;
                case "blockmultiedges":
                    if (!Misc.TryConvertToBool(value, out bool blockMultiedgesValue))
                        return OperationResult.Fail("InvalidValue", $"Setting 'blockmultiedges' requires a boolean value ('true'/'false'), got '{value}'.");
                    BlockMultiedges = blockMultiedgesValue;
                    break;

                case "maxthreads":
                    if (!Misc.TryConvertToInt(value, out int maxThreadsValue))
                        return OperationResult.Fail("InvalidValue", $"Setting 'maxthreads' requires an integer value, got '{value}'.");
                    if (maxThreadsValue != -1 && maxThreadsValue < 1)
                        return OperationResult.Fail("InvalidValue", "Setting 'maxthreads' must be -1 (unconstrained) or a positive integer.");
                    if (maxThreadsValue > Environment.ProcessorCount)
                        return OperationResult.Fail("InvalidValue", $"Setting 'maxthreads' ({maxThreadsValue}) exceeds the number of available processor cores ({Environment.ProcessorCount}). Use 'systeminfo()' to check available cores.");
                    if (MaxThreadsLimit != -1 && maxThreadsValue > MaxThreadsLimit)
                        return OperationResult.Fail("InvalidValue", $"Setting 'maxthreads' ({maxThreadsValue}) exceeds the limit ({MaxThreadsLimit}) set by 'maxthreadslimit' in {AppConfig.FileName}.");
                    if (maxThreadsValue == -1 && MaxThreadsLimit != -1)
                    {
                        MaxDegreeOfParallelism = EffectiveMaxThreadsLimit;
                        return OperationResult.Ok($"Setting 'maxthreads' to {MaxDegreeOfParallelism}, the limit set by 'maxthreadslimit' in {AppConfig.FileName}.");
                    }
                    MaxDegreeOfParallelism = maxThreadsValue;
                    break;

                default:
                    return OperationResult.Fail("SettingNotFound", $"Unknown setting: '{key}'.");
            }
            return OperationResult.Ok($"Setting '{key}' to {value}.");
        }

        /// <summary>
        /// The highest value that 'maxthreads' can currently be set to: the number of available processor
        /// cores, or <see cref="MaxThreadsLimit"/> if that is set and lower.
        /// </summary>
        public static int EffectiveMaxThreadsLimit => MaxThreadsLimit == -1 ? Environment.ProcessorCount : Math.Min(MaxThreadsLimit, Environment.ProcessorCount);

        /// <summary>
        /// Builds a <see cref="System.Threading.Tasks.ParallelOptions"/> instance reflecting the current
        /// <see cref="MaxDegreeOfParallelism"/> setting, for use by parallelized analysis methods.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.ParallelOptions"/> object with MaxDegreeOfParallelism set accordingly.</returns>
        public static System.Threading.Tasks.ParallelOptions GetParallelOptions() => new() { MaxDegreeOfParallelism = MaxDegreeOfParallelism };
        #endregion
    }
}