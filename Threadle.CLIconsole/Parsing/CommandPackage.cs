namespace Threadle.CLIconsole.Parsing
{
    /// <summary>
    /// Class representing a CLI package, i.e. the command and its content (arguments and assigned variable if relevant).
    /// </summary>
    public class CommandPackage
    {
        #region Properties
        /// <summary>
        /// The variable name that the output from this command should be assigned to.
        /// </summary>
        public string? AssignedVariable { get; set; }

        /// <summary>
        /// The command (such as 'addnode', 'density' etc)
        /// </summary>
        public string CommandName { get; set; } = "";

        /// <summary>
        /// Dictionary of named arguments in the command (such as 'nodeid'=>'13', 'arg2'=>'mynet.tsv')
        /// For compulsory (non-optional) arguments, this also contains argument values based on the location
        /// of their keys (i.e. 'arg0', 'arg1' etc).
        /// </summary>
        public Dictionary<string, string> NamedArgs { get; set; } = new();
        #endregion

        #region Methods (internal)
        /// <summary>
        /// Support function to check whether a command must be assigned (i.e. returning either a network or nodeset),
        /// or whether it is not. Used centrally by CommandDispatcher.
        /// </summary>
        /// <param name="toAssign">Boolean to indicate whether a command is to be assigned (true) or not (false).</param>
        /// <exception cref="Exception">Throws an exception if an assignment is either missing or present when it should not be there.</exception>
        internal void CheckAssigment(bool toAssign)
        {
            if (toAssign && AssignedVariable == null)
                throw new Exception($"!Error: '{CommandName}()' must be assigned.");
            else if (!toAssign && AssignedVariable != null)
                throw new Exception($"!Error: '{CommandName}()' not for assigning.");
        }

        /// <summary>
        /// Checks that a variable is assigned to the command, and returns this.
        /// Throws an exception if the command is not assigned to a variable.
        /// </summary>
        /// <returns>The name of the variable.</returns>
        /// <exception cref="Exception">Throws an exception if no variable is assigned.</exception>
        internal string GetAssignmentVariableNameThrowExceptionIfNull()
        {
            if (AssignedVariable == null)
                throw new Exception("!Error: No variable assigned.");
            return AssignedVariable.Trim();
        }

        /// <summary>
        /// Returns the value of an argument as a string if it exists; otherwise returns null.
        /// </summary>
        /// <param name="key">The argument name.</param>
        /// <returns></returns>
        internal string? GetArgument(string key)
        {
            if (NamedArgs.TryGetValue(key, out var value))
                return value;
            return null;
        }

        /// <summary>
        /// Returns the value of an argument as a string if it exists; if not, checks an alternative key.
        /// If that doesn't exist either, return null
        /// </summary>
        /// <param name="key">The first argument name to check.</param>
        /// <param name="altkey">The alternative argument name to check.</param>
        /// <returns>The string value of this argument, or null if missing.</returns>
        internal string? GetArgument(string key, string altkey)
        {
            if (NamedArgs.TryGetValue(key, out var value))
                return value;
            else if (NamedArgs.TryGetValue(altkey, out var value2))
                return value2;
            return null;
        }

        /// <summary>
        /// Gets the value of argument 'key' (or 'altkey' if 'key' is missing). Throws an
        /// exception if neither is found, or if it is found but where the argument value is empty or null.
        /// </summary>
        /// <param name="key">The first argument name to look for.</param>
        /// <param name="altKey">The second argument name to look for.</param>
        /// <returns>The value of the argument (as a string).</returns>
        /// <exception cref="Exception">Throws an exception if the argument is not found.</exception>
        internal string GetArgumentThrowExceptionIfMissingOrNull(string key, string? altKey = null)
        {
            if (!NamedArgs.TryGetValue(key, out var value) && altKey is not null && !NamedArgs.TryGetValue(altKey, out value))
                throw new Exception($"!Error: Argument '{key}' missing.");
            if (string.IsNullOrEmpty(value))
                throw new Exception($"!Error: Argument '{key}' missing or empty.");
            return value;
        }

        /// <summary>
        /// Gets the string value of argument 'key'. If not found, the provided default string value is returned.
        /// </summary>
        /// <param name="key">The argument name to look for.</param>
        /// <param name="defaultValue">The default string value if the argument name is not found</param>
        /// <returns>The string value of the argument, or the default value if the argument is not found.</returns>
        internal string GetArgumentParseString(string key, string defaultvalue)
        {
            if (!(GetArgument(key) is string value))
                return defaultvalue;
            return value;
        }

        /// <summary>
        /// Gets the integer value of argument 'key'. If not found, the provided default integer value is returned.
        /// </summary>
        /// <param name="key">The argument name to look for.</param>
        /// <param name="defaultValue">The default integer value if the argument name is not found.</param>
        /// <returns>The integer value of the argument, or the default value if the argument is not found.</returns>
        internal int GetArgumentParseInt(string key, int defaultValue)
        {
            if (int.TryParse(GetArgument(key), out int value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Gets the double value of argument 'key'. If not found, the provided default double value is returned.
        /// </summary>
        /// <param name="key">The argument name to look for.</param>
        /// <param name="defaultValue">The default double value if the argument name is not found.</param>
        /// <returns>The double value of the argument, or the default value if the argument is not found.</returns>
        internal double GetArgumentParseDouble(string key, double defaultValue)
        {
            if (double.TryParse(GetArgument(key), out double value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Gets the integer value of argument 'key' (or 'altkey' if 'key' is missing). Throws an
        /// exception if neither is found.
        /// </summary>
        /// <param name="key">The first argument name to look for.</param>
        /// <param name="altKey">The second argument name to look for.</param>
        /// <returns>The value of the argument (as an integer).</returns>
        /// <exception cref="Exception">Throws an exception if the argument is not found.</exception>
        internal int GetArgumentParseIntThrowExceptionIfMissingOrNull(string key, string? altKey)
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!int.TryParse(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' not an integer");
            return value;
        }

        /// <summary>
        /// Gets the unsigned integer value of argument 'key' (or 'altkey' if 'key' is missing). Throws an
        /// exception if neither is found.
        /// </summary>
        /// <param name="key">The first argument name to look for.</param>
        /// <param name="altKey">The second argument name to look for.</param>
        /// <returns>The value of the argument (as an unsigned integer).</returns>
        /// <exception cref="Exception">Throws an exception if the argument is not found.</exception>
        internal uint GetArgumentParseUintThrowExceptionIfMissingOrNull(string key, string? altKey)
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!uint.TryParse(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' not an unsigned integer");
            return value;
        }

        /// <summary>
        /// Gets the floating-point (double) value of argument 'key'. If not found, the provided default string value is returned.
        /// </summary>
        /// <param name="key">The argument name to look for.</param>
        /// <param name="defaultValue">The default double value if the argument name is not found.</param>
        /// <returns>The double value of the argument, or the default value if the argument is not found.</returns>
        internal float GetArgumentParseFloat(string key, float defaultValue)
        {
            if (float.TryParse(GetArgument(key), out float value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Gets the floating-point (double) value of argument 'key' (or 'altkey' if 'key' is missing).
        /// Throws an exception if neither is found.
        /// </summary>
        /// <param name="key">The first argument name to look for.</param>
        /// <param name="altKey">The second argument name to look for.</param>
        /// <returns>The value of the argument (as a double).</returns>
        /// <exception cref="Exception">Throws an exception if the argument is not found.</exception>
        internal double GetArgumentParseDoubleThrowExceptionIfMissingOrNull(string key, string? altKey)
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!double.TryParse(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' not a floating point");
            return value;
        }

        /// <summary>
        /// Gets the boolean value of argument 'key'. If not found, the provided default value is returned.
        /// </summary>
        /// <param name="key">The argument name to look for.</param>
        /// <param name="defaultValue">The default boolean value if the argument name is not found</param>
        /// <returns>The boolean value of the argument, or the default value if the argument is not found.</returns>
        internal bool GetArgumentParseBool(string key, bool defaultValue)
        {
            if (bool.TryParse(GetArgument(key), out bool value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Gets the boolean value of argument 'key' (or 'altkey' if 'key' is missing).
        /// Throws an exception if neither is found.
        /// </summary>
        /// <param name="key">The first argument name to look for.</param>
        /// <param name="altKey">The second argument name to look for.</param>
        /// <returns>The value of the argument (as a boolean).</returns>
        /// <exception cref="Exception">Throws an exception if the argument is not found.</exception>
        internal bool GetArgumentParseBoolThrowExceptionIfMissingOrNull(string key, string altKey)
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!bool.TryParse(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' neither 'true' nor 'false'.");
            return value;
        }

        /// <summary>
        /// Gets the value of argument 'key' for Enum type T. If not found, the provided default Enum value is returned.
        /// </summary>
        /// <typeparam name="T">The Enum type.</typeparam>
        /// <param name="key">The argument name to look for.</param>
        /// <param name="defaultValue">The default Enum value.</param>
        /// <returns>The Enum value of the argument for Enum T, or the default value if the argument is not found.</returns>
        internal T GetArgumentParseEnum<T>(string key, T defaultValue) where T : struct, Enum
        {
            if (Enum.TryParse<T>(GetArgument(key), true, out var value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Gets the value of argument 'key' for Enum type T (or 'altkey' if 'key' is missing).
        /// Throws an exception if neither is found.
        /// </summary>
        /// <typeparam name="T">The Enum type.</typeparam>
        /// <param name="key">The first argument name to look for.</param>
        /// <param name="altKey">The second argument name to look for.</param>
        /// <returns>The Enum value of the argument for Enum T.</returns>
        /// <exception cref="Exception">Throws an exception if the argument is not found.</exception>
        internal T GetArgumentParseEnumThrowExceptionIfMissingOrNull<T>(string key, string altKey) where T : struct, Enum
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!Enum.TryParse<T>(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' not a valid {typeof(T).Name}.");
            return value;
        }

        /// <summary>
        /// Support function to trim the name of a layer or node attribute. Trims whitespace at start
        /// and end.
        /// Names are kept case-sensitive, and only alphanumericals and underscore characters are allowed.
        /// The First letter must also be an alphanumerical. No whitespace allowed inside.
        /// If other characters are present, the method returns false.
        /// </summary>
        /// <param name="input">String to trim and check.</param>
        /// <param name="output">String output (trimmed)</param>
        /// <returns>Returns true if string contains unallowed characters.</returns>
        internal bool TrimNameAndCheckValidity(string input, out string output)
        {
            output = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
                return false;
            output = input.Trim();
            if (!char.IsLetter(output[0]))
                return false;
            foreach (char c in output)
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            return true;
        }
        #endregion
    }
}
