using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static T ParseEnumOrNull<T>(string? value, T defaultValue) where T: struct, Enum
        {
            if (Enum.TryParse<T>(value, true, out var result))
                return result;
            return defaultValue;
        }

        public static ulong GetNbrPotentialEdges(ulong n, bool directed, bool selfties)
        {
            if (directed)
                return selfties ? n * n : n * (n - 1);
            else
                return selfties ? n * (n + 1) / 2 : n * (n - 1) / 2;
        }

        //public static T? GetRandom<T>(this List<T> list)
        //{
        //    if (list == null || list.Count == 0)
        //        throw new InvalidOperationException("Cannot select a random item from an empty list.");
        //    return list[Random.Next(list.Count)];
        //}

        public static T? GetRandom<T>(this T[] array)
        {
            if (array == null || array.Length == 0)
                throw new InvalidOperationException("Cannot select a random item from an empty array.");
            return array[Random.Next(array.Length)];
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


        //public static ulong GetTotalNbrOutgoingEdges(LayerOneMode layer)
        //{
        //    ulong sum = 0;
        //    //foreach (IConnectionCollection connection in layer.Connections.Values)
        //    //    sum += (ulong)connection.GetOutboundConnections().Count;
        //    foreach (IEdgeset edgeset in layer.Edgesets.Values)
        //        sum += (ulong)edgeset.NbrOutboundEdges;
        //    return sum;
        //}

        public static float FixConnectionValue(float value, EdgeType valueType)
            => valueType switch
            {
                EdgeType.Binary => value > 0 ? 1 : 0,
                //EdgeValueType.Signed => value < 0 ? -1 : value > 0 ? 1 : 0,
                _ => value
            };

        internal static bool HasNonzeroDiagonal(float[,] data)
        {
            for (int i = 0; i < data.GetLength(0); i++)
                if (data[i, i] != 0)
                    return true;
            return false;
        }

        internal static EdgeDirectionality GetDirectionality(float[,] data)
        {
            for (int r = 0; r < data.GetLength(0); r++)
                for (int c = r + 1; c < data.GetLength(1); c++)
                    if (data[r, c] != data[c, r])
                        return EdgeDirectionality.Directed;
            return EdgeDirectionality.Undirected;
        }

        internal static EdgeType GetValueType(float[,] data)
        {
            //bool hasMinusOne = false;
            for (int r=0; r < data.GetLength(0);r++)
                for (int c=0;c<data.GetLength(1);c++)
                {
                    if (data[r, c] != 0 && data[r, c] != 1 && data[r, c] != -1)
                        return EdgeType.Valued;
                    //if (data[r, c] == -1)
                    //    hasMinusOne = true;
                }
            //if (hasMinusOne)
            //    return EdgeValueType.Signed;
            return EdgeType.Binary;
        }

        //internal static double GetMean(List<double> list)
        //{
        //    return list.Select(b => (double)b).Average();
        //}

        //internal static double GetVariance(List<double> list)
        //{
        //    double mean = GetMean(list);
        //    return list.Select(b => Math.Pow((double)b - mean, 2)).Average();
        //}

        //internal static double GetStandardDeviation(List<double> list)
        //{
        //    return Math.Sqrt(GetVariance(list));
        //}

        public static Network MergeNetworks(List<Network> networks)
        {
            Nodeset nodeset = networks[0].Nodeset;
            Network mergedNetwork = new Network("merged", nodeset);
            //byte layerIndex = 0;
            //foreach (NetworkModel network in networks)
            //{
            //    foreach ((byte layerId, LayerOneMode layer) in network.LayersOneMode)
            //    {
            //        layer.Name = network.Name + "_" + layerId;
            //        mergedNetwork.LayersOneMode.Add(layerIndex, layer);
            //        layerIndex++;
            //    }
            //}
            return mergedNetwork;
        }

        public static float[,] ConvertStringCellsToFloatCells(string[,] cells, int startOffset)
        {
            int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            float[,] data = new float[nbrRows - startOffset, nbrCols - startOffset];
            for (int r = 0; r < nbrRows - startOffset; r++)
                for (int c = 0; c < nbrCols - startOffset; c++)
                    float.TryParse(cells[r + startOffset, c + startOffset], out data[r, c]);
            return data;
        }


        internal static List<double> ToDoubleList(List<int> list)
        {
            return list.Select(b => (double)b).ToList();
        }

        //internal static double GetMedian(List<double> list)
        //{
        //    List<double> sorted = list.OrderBy(n => n).ToList();
        //    int count = sorted.Count;
        //    if (count % 2 == 1)
        //        return sorted[count / 2];
        //    else
        //        return (sorted[(count / 2) - 1] - sorted[(count / 2)]) / 2.0;
        //}

        public static EdgeType GetValueTypeFromString(string? valueString, EdgeType defaultValueType = EdgeType.Binary)
        {
            if (valueString == null)
                return defaultValueType;
            if (valueString.Equals("binary", StringComparison.OrdinalIgnoreCase))
                return EdgeType.Binary;
            if (valueString.Equals("valued", StringComparison.OrdinalIgnoreCase))
                return EdgeType.Valued;
            //if (valueString.Equals("signed", StringComparison.OrdinalIgnoreCase))
            //    return EdgeValueType.Signed;
            return defaultValueType;
        }

        public static ConditionType? GetConditionTypeFromString(string conditionString)
        {
            return conditionString.ToLower() switch
            {
                "eq" => ConditionType.eq,
                "ne" => ConditionType.ne,
                "gt" => ConditionType.gt,
                "ge" => ConditionType.ge,
                "lt" => ConditionType.lt,
                "le" => ConditionType.le,
                "isnull" => ConditionType.isnull,
                "notnull" => ConditionType.notnull,
                _ => null
            };
        }

        public static uint[] NodesIdsStringToArray(string nodesString, char sep = ';')
        {
            return nodesString.Split(sep).Select(s => uint.Parse(s)).ToArray();
        }

        public static string BooleanAsString(bool check)
        {
            return check ? "true" : "false";
        }

        //internal static List<string> GenerateNodelistChunk(LayerOneMode layerOneMode)
        //{
        //    List<string> lines = [];
        //    foreach ((uint nodeid, IEdgeSet edgeset) in layerOneMode.Edgesets)
        //    {

        //    }

        //    return lines;
        //}
    }
}
