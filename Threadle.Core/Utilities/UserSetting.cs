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
        #endregion


        #region Methods
        /// <summary>
        /// Method for modifying the value of a setting.
        /// </summary>
        /// <param name="key">The name of the setting (in lowercase).</param>
        /// <param name="value">The boolean value of the setting.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how this went.</returns>
        public static OperationResult Set(string key, bool value)
        {
            if (key.Equals("nodecache"))
                NodeCache = value;
            else if (key.Equals("blockmultiedges"))
                BlockMultiedges = value;
            else if (key.Equals("onlyoutboundedges"))
                OnlyOutboundEdges = value;
            else
                return OperationResult.Fail("SettingNotFound", $"Unknown setting: '{key}'.");
            return OperationResult.Ok($"Setting '{key}' to {value}.");
        }
        #endregion
    }
}
