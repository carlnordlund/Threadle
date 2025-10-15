using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.CLIUtilities
{
    public class CommandContext
    {
        public Dictionary<string, IStructure> Variables { get; } = new();

        public void SetVariable(string name, IStructure value, bool renameIfExist = false)
        {
            Variables[name.ToLowerInvariant()] = value;
        }

        public IStructure? GetVariable(string name)
        {
            Variables.TryGetValue(name, out var value);
            return value;
        }

        public void DeleteStructures(IEnumerable<IStructure> structures)
        {
            List<string> keysToRemove = new();
            foreach (var kvp in Variables)
                if (structures.Contains(kvp.Value))
                    keysToRemove.Add(kvp.Key);
            foreach (string key in keysToRemove)
                Variables.Remove(key);
        }

        public OperationResult RemoveStructure(string structureName)
        {
            if (!(GetVariable(structureName) is IStructure structure))
                return OperationResult.Fail("StructureNotFound", $"Structure {structureName} not found.");
            if (structure is Nodeset nodeset)
            {
                foreach (var kvp in Variables)
                    if (kvp.Value is Threadle.Core.Model.Network network && network.Nodeset == nodeset)
                        return OperationResult.Fail("NodesetInUse", $"Can not delete nodeset '{structureName}': used by network '{kvp.Key}'.");
            }
            Variables.Remove(structureName);
            return OperationResult.Ok($"Structure '{structureName}' removed.");
        }

        public void RemoveAllStructures()
        {
            Variables.Clear();
        }

        public T? GetVariable<T>(string name) where T : class
        {
            if (Variables.TryGetValue(name, out var value) && value is T typedValue)
                return typedValue;
            return null;
        }

        public T GetVariableThrowExceptionIfMissing<T>(string name) where T: class
        {
            if (Variables.TryGetValue(name, out var value) && value is T typedValue)
                return typedValue;
            throw new Exception($"!Error: No {typeof(T).Name} named '{name}' found.");
        }

        public bool VariableExists(string name)
        {
            return Variables.ContainsKey(name);
        }

        internal string GetNextIncrementalName(string baseName = "Untitled-")
        {
            int i = 0;
            while (true)
            {
                if (!VariableExists(baseName + i))
                    return "Untitled-" + i;
                i++;
            }
        }

        internal Nodeset GetNodesetFromIStructure(string structureName)
        {
            Nodeset nodeset = GetVariableThrowExceptionIfMissing<IStructure>(structureName) switch
            {
                Nodeset ns => ns,
                Network net => net.Nodeset,
                _ => throw new ArgumentException($"Structure '{structureName}' neither a Nodeset nor a Network.")
            };
            return nodeset;

        }
    }
}
