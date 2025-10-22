using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    public class Command
    {
        // OutputVariable: the variable that something is assigned to
        public string? AssignedVariable { get; set; }

        // CommandName, like "createnetwork"
        public string CommandName { get; set; } = "";

        public Dictionary<string, string> NamedArgs { get; set; } = new();

        internal void CheckAssignment(bool mustAssign)
        {
            if (mustAssign && AssignedVariable == null)
                throw new Exception($"!Error: '{CommandName}()' must be assiged.");
            else if (!mustAssign && AssignedVariable != null)
                throw new Exception($"!Error: '{CommandName}()' not for assigning.");
        }

        internal string? GetArgument(string key)
        {
            if (NamedArgs.TryGetValue(key, out var value))
                return value;
            return null;
        }

        internal string? GetArgument(string key1, string key2)
        {
            if (NamedArgs.TryGetValue(key1, out var value1))
                return value1;
            if (NamedArgs.TryGetValue(key2, out var value2))
                return value2;
            return null;
        }

        internal string GetArgumentThrowExceptionIfMissingOrNull(string key, string altKey)
        {
            if (!(GetArgument(key, altKey) is string value) || value == string.Empty)
                throw new Exception($"!Error: Argument '{key}' missing");
            return value;
        }


        internal int GetArgumentParseIntThrowExceptionIfMissingOrNull(string key, string altKey)
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!int.TryParse(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' not an integer");
            return value;
        }

        internal uint GetArgumentParseUintThrowExceptionIfMissingOrNull(string key, string altKey)
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!uint.TryParse(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' not an unsigned integer");
            return value;
        }


        internal double GetArgumentParseDoubleThrowExceptionIfMissingOrNull(string key, string altKey)
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!double.TryParse(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' not a floating point");
            return value;
        }

        internal bool GetArgumentParseBool(string key, bool defaultValue)
        {
            if (bool.TryParse(GetArgument(key), out bool value))
                return value;
            return defaultValue;
        }

        internal string GetArgumentParseString(string key, string defaultvalue)
        {
            if (!(GetArgument(key) is string value))
                return defaultvalue;
            return value;
        }

        internal T GetArgumentParseEnum<T>(string key, T defaultValue) where T : struct, Enum
        {
            if (Enum.TryParse<T>(GetArgument(key), out var value))
                return value;
            return defaultValue;
        }

        internal T GetArgumentParseEnumThrowExceptionIfMissingOrNull<T>(string key, string altKey) where T : struct, Enum
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!Enum.TryParse<T>(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' not a valid {typeof(T).Name}.");
            return value;
        }

        internal bool GetArgumentParseBoolThrowExceptionIfMissingOrNull(string key, string altKey)
        {
            string valueString = GetArgumentThrowExceptionIfMissingOrNull(key, altKey);
            if (!bool.TryParse(valueString, out var value))
                throw new Exception($"!Error: Value '{valueString}' neither 'true' nor 'false'.");
            return value;
        }

        internal int GetArgumentParseInt(string key, int defaultValue)
        {
            if (int.TryParse(GetArgument(key), out int value))
                return value;
            return defaultValue;
        }

        internal float GetArgumentParseFloat(string key, float defaultValue)
        {
            if (float.TryParse(GetArgument(key), out float value))
                return value;
            return defaultValue;
        }

        internal string CheckAndGetAssignmentVariableName()
        {
            if (AssignedVariable == null)
                throw new Exception("!Error: No variable assigned.");
            return AssignedVariable.Trim().ToLowerInvariant();
        }

        internal bool NormalizeNameAndCheckValidity(string input, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
                return false;
            normalized = input.Trim().ToLowerInvariant();
            if (!char.IsLetter(normalized[0]))
                return false;
            foreach (char c in normalized)
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            return true;
        }
    }
}
