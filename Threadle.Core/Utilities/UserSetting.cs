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
        /// Setting determining if the storing of directed edges (in directed 1-mode layers) should only
        /// be stored as outgoing edges, i.e. so that an ego node only has references to nodes that it has
        /// connections TO, not the nodes that the ego node has connections FROM. This might be useful for some
        /// operations, e.g. for random walkers in layers with directed edges, where it then lowers the memory
        /// footprint by half. This only has an effect on directed 1-mode layers.
        /// </summary>
        public static bool OnlyOutboundEdges { get; set; } = false;

        /// <summary>
        /// Setting for the maximum number of threads that parallelized methods (e.g. betweenness centrality)
        /// are allowed to use concurrently. -1 (the default) means unconstrained, i.e. .NET decides based on
        /// the number of available cores. Any other value must be a positive integer no greater than
        /// Environment.ProcessorCount, capping the number of threads used, e.g. to leave cores free for other work.
        /// </summary>
        public static int MaxDegreeOfParallelism { get; set; } = -1;
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

                case "onlyoutboundedges":
                    if (!Misc.TryConvertToBool(value, out bool onlyOutboundEdgesValue))
                        return OperationResult.Fail("InvalidValue", $"Setting 'onlyoutboundedges' requires a boolean value ('true'/'false'), got '{value}'.");
                    OnlyOutboundEdges = onlyOutboundEdgesValue;
                    break;

                case "maxthreads":
                    if (!Misc.TryConvertToInt(value, out int maxThreadsValue))
                        return OperationResult.Fail("InvalidValue", $"Setting 'maxthreads' requires an integer value, got '{value}'.");
                    if (maxThreadsValue != -1 && maxThreadsValue < 1)
                        return OperationResult.Fail("InvalidValue", "Setting 'maxthreads' must be -1 (unconstrained) or a positive integer.");
                    if (maxThreadsValue > Environment.ProcessorCount)
                        return OperationResult.Fail("InvalidValue", $"Setting 'maxthreads' ({maxThreadsValue}) exceeds the number of available processor cores ({Environment.ProcessorCount}). Use 'system()' to check available cores.");

                    MaxDegreeOfParallelism = maxThreadsValue;
                    break;

                default:
                    return OperationResult.Fail("SettingNotFound", $"Unknown setting: '{key}'.");
            }

            //if (key.Equals("nodecache"))
            //    NodeCache = value;
            //else if (key.Equals("blockmultiedges"))
            //    BlockMultiedges = value;
            //else if (key.Equals("onlyoutboundedges"))
            //    OnlyOutboundEdges = value;
            //else
            //    return OperationResult.Fail("SettingNotFound", $"Unknown setting: '{key}'.");
            return OperationResult.Ok($"Setting '{key}' to {value}.");
        }

        /// <summary>
        /// Builds a <see cref="System.Threading.Tasks.ParallelOptions"/> instance reflecting the current
        /// <see cref="MaxDegreeOfParallelism"/> setting, for use by parallelized analysis methods.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.ParallelOptions"/> object with MaxDegreeOfParallelism set accordingly.</returns>
        public static System.Threading.Tasks.ParallelOptions GetParallelOptions() => new() { MaxDegreeOfParallelism = MaxDegreeOfParallelism };
        #endregion
    }
}
