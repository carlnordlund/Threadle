using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// All these methods are useful methods for working with networks, layers, edgesets etc
    /// But for actual mathematical analyses of networks, supporting Analyses.cs, these are
    /// instead found in Analysis/Functions.cs
    /// </summary>
    public static class Misc
    {
        public static readonly Random Random = new Random();

        public static bool EvalutateCondition(NodeAttributeValue nodeValue, string comparisonValue, ConditionType condition)
        {
            switch (nodeValue.Type)
            {
                case NodeAttributeType.Int:
                    if (!int.TryParse(comparisonValue, out int intVal))
                        return false;
                    return CompareValues(Convert.ToInt32(nodeValue.GetValue()), intVal, condition);
                case NodeAttributeType.Float:
                    if (!float.TryParse(comparisonValue, out float floatVal))
                        return false;
                    return CompareValues(Convert.ToSingle(nodeValue.GetValue()), floatVal, condition);
                case NodeAttributeType.Bool:
                    if (!bool.TryParse(comparisonValue, out bool boolVal))
                        return false;
                    return CompareValues(Convert.ToBoolean(nodeValue.GetValue()), boolVal, condition);
                case NodeAttributeType.Char:
                    if (comparisonValue.Length != 1)
                        return false;
                    return CompareValues(Convert.ToChar(nodeValue.GetValue()), comparisonValue[0], condition);
                default:
                    return false;
            }
        }

        public static bool CompareValues<T>(T nodeVal, T compVal, ConditionType condition) where T : IComparable<T>
        {
            int cmp = nodeVal.CompareTo(compVal);
            return condition switch
            {
                ConditionType.eq => cmp == 0,
                ConditionType.ne => cmp != 0,
                ConditionType.gt => cmp > 0,
                ConditionType.ge => cmp >= 0,
                ConditionType.lt => cmp < 0,
                ConditionType.le => cmp <= 0,
                _ => false
            };
        }

        /// <summary>
        /// Creates and returns a <see cref="NodeAttributeValue"/> struct based on the given <see cref="NodeAttributeType"/> and value string.
        /// If the value string can't be parsed to the specified attribute type, null is returned.
        /// </summary>
        /// <param name="attrType">The <see cref="NodeAttributeType"/> of this node attribute.</param>
        /// <param name="valueStr">The value (in string format) of the attribute for the specific node.</param>
        /// <returns>A <see cref="NodeAttributeValue"/> struct with the specified attribute type (<see cref="NodeAttributeType">) and provided value.
        /// Returns null if the <paramref name="valueStr"/> can not be parsed to the specified <see cref="NodeAttributeType"/>.</returns>
        public static NodeAttributeValue? CreateNodeAttributeValueFromAttributeTypeAndValueString(NodeAttributeType attrType, string valueStr)
        {
            if (attrType == NodeAttributeType.Int && Int32.TryParse(valueStr, out int i))
                return new NodeAttributeValue(i);
            if (attrType == NodeAttributeType.Float && float.TryParse(valueStr, out float f))
                return new NodeAttributeValue(f);
            if (attrType == NodeAttributeType.Char && char.TryParse(valueStr, out char c))
                return new NodeAttributeValue(c);
            if (attrType == NodeAttributeType.Bool && bool.TryParse(valueStr, out bool b))
                return new NodeAttributeValue(b);
            return null;
        }

        public static ulong GetNbrPotentialEdges(ulong n, EdgeDirectionality directionality, bool selfties)
        {
            if (directionality == EdgeDirectionality.Directed)
                return selfties ? n * n : n * (n - 1);
            else
                return selfties ? n * (n + 1) / 2 : n * (n - 1) / 2;
        }



        /// <summary>
        /// Internal helper function for converting textual representation of a type of node attribute to
        /// a <see cref="NodeAttributeType"/> Enum. If the attributeTypeName is not recognized, it returns null
        /// </summary>
        /// <remarks>
        /// Rather than a switch statement, this could also be done using a Enum.TryParse() statement, but
        /// the switch was chosen for clarity.
        /// </remarks>
        /// <param name="attributetype">The textual representation of this node attribute type (either 'char','int','float', or 'bool').</param>
        /// <returns>The corresponding <see cref="NodeAttributeType"> value.</returns>
        public static NodeAttributeType? GetAttributeType(string attributeTypeName)
            => attributeTypeName.ToLowerInvariant() switch
            {
                "char" => NodeAttributeType.Char,
                "int" => NodeAttributeType.Int,
                "float" => NodeAttributeType.Float,
                "bool" => NodeAttributeType.Bool,
                _ => null
            };

        public static float FixConnectionValue(float value, EdgeType valueType)
            => valueType switch
            {
                EdgeType.Binary => value > 0 ? 1 : 0,
                _ => value
            };

        public static float[,] ConvertStringCellsToFloatCells(string[,] cells, int startOffset)
        {
            int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            float[,] data = new float[nbrRows - startOffset, nbrCols - startOffset];
            for (int r = 0; r < nbrRows - startOffset; r++)
                for (int c = 0; c < nbrCols - startOffset; c++)
                    float.TryParse(cells[r + startOffset, c + startOffset], out data[r, c]);
            return data;
        }

        public static uint[] NodesIdsStringToArray(string nodesString, char sep = ';')
        {
            return nodesString.Split(sep).Select(s => uint.Parse(s)).ToArray();
        }

        public static string BooleanAsString(bool check)
        {
            return check ? "true" : "false";
        }

        internal static ulong SampleGeometric(double p)
        {
            return (ulong)Math.Floor(Math.Log(Misc.Random.NextDouble()) / Math.Log(1.0 - p));
        }

        internal static string BetweenFromToText(EdgeDirectionality directionality, uint node1id, uint node2id)
        {
            return directionality == EdgeDirectionality.Directed ? $"from {node1id} to {node2id}" : $"between {node1id} and {node2id}";
        }
    }
}
