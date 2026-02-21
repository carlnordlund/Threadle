using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing.Enums;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// All these methods are useful methods for working with networks, layers, edgesets etc
    /// But for actual mathematical analyses of networks, supporting Analyses.cs, these are
    /// instead found in Analysis/Functions.cs
    /// </summary>
    public static class Misc
    {
        #region Fields
        /// <summary>
        /// Provides an instance of the random number generator.
        /// </summary>
        private static Random _random = new Random();

        /// <summary>
        /// Collection of characters that could pop up in value quoting in csv files
        /// </summary>
        private static char[] _quoteChars = new char[] {
            '"',
            '\'',
            '\u201C',   // Left double quotes
            '\u201D',   // Right double quote
            '\u2018',   // Left single quote
            '\u2019'    // Right single quote
        };
        #endregion


        #region Properties
        /// <summary>
        /// Gets a shared instance of the <see cref="Random"/> class for generating random numbers.
        /// </summary>
        public static Random Random => _random;
        #endregion



        #region Methods (public)
        /// <summary>
        /// Method to set the random seed of the RNG used here in Misc and Threadle at large.
        /// Used for replication purposes.
        /// </summary>
        /// <param name="seed">The seed to set the random number generator to</param>
        public static void SetRandomSeed(int seed)
        {
            _random = new Random(seed);
        }


        /// <summary>
        /// Evaluates a condition by comparing the value of a node attribute to a specified comparison value using the
        /// provided condition type.
        /// </summary>
        /// <param name="nodeValue">The value of the node attribute to evaluate. The type of the value is determined by the <see
        /// cref="NodeAttributeValue.Type"/> property.</param>
        /// <param name="comparisonValue">The value to compare against as a string type.</param>
        /// <param name="condition">The <see cref="ConditionType"/> to evaluate.</param>
        /// <returns><see langword="true"/> if the condition is satisfied based on the comparison; otherwise, <see
        /// langword="false"/>.</returns>
        public static bool EvaluateCondition(NodeAttributeValue nodeValue, string comparisonValue, ConditionType condition)
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

        /// <summary>
        /// Compares two values of a specified type based on the provided condition.
        /// </summary>
        /// <remarks>This method uses the <see cref="IComparable{T}.CompareTo"/> method to perform the
        /// comparison. Supported conditions include equality, inequality, greater than, greater than or equal to, less
        /// than, and less than or equal to.</remarks>
        /// <typeparam name="T">The type of the values to compare. Must implement <see cref="IComparable{T}"/>.</typeparam>
        /// <param name="nodeVal">The first value to compare.</param>
        /// <param name="compVal">The second value to compare.</param>
        /// <param name="condition">The condition to evaluate the comparison. Must be one of the values defined in <see cref="ConditionType"/>.</param>
        /// <returns><see langword="true"/> if the comparison satisfies the specified condition; otherwise, <see
        /// langword="false"/>.</returns>
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

        /// <summary>
        /// Calculates the number of potential edges in a graph based on the number of nodes,  edge directionality, and
        /// whether self-loops are allowed.
        /// </summary>
        /// <param name="n">The number of nodes in the graph. Must be a non-negative value.</param>
        /// <param name="directionality">Specifies whether the graph is directed or undirected using <see cref="EdgeDirectionality"/> values.</param>
        /// <param name="selfties">A boolean value indicating whether self-loops are allowed.</param>
        /// <returns>The total number of potential edges in the graph. This value is calculated based on the  specified number of
        /// nodes, directionality, and self-loop allowance.</returns>
        public static ulong GetNbrPotentialEdges(ulong n, EdgeDirectionality directionality, bool selfties)
        {
            if (directionality == EdgeDirectionality.Directed)
                return selfties ? n * n : n * (n - 1);
            else
                return selfties ? n * (n + 1) / 2 : n * (n - 1) / 2;
        }

        /// <summary>
        /// Helper methods for converting textual representation of a type of node attribute to
        /// a <see cref="NodeAttributeType"/> Enum. If the attributeTypeName is not recognized, it returns null
        /// </summary>
        /// <remarks>
        /// Rather than a switch statement, this could also be done using a Enum.TryParse() statement, but
        /// the switch was chosen for clarity.
        /// </remarks>
        /// <param name="attributeTypeName">The textual representation of this node attribute type (either 'char','int','float', or 'bool').</param>
        /// <returns>The corresponding <see cref="NodeAttributeType"/> value.</returns>
        public static NodeAttributeType? GetAttributeType(string attributeTypeName)
            => attributeTypeName.ToLowerInvariant() switch
            {
                "char" => NodeAttributeType.Char,
                "int" => NodeAttributeType.Int,
                "float" => NodeAttributeType.Float,
                "bool" => NodeAttributeType.Bool,
                _ => null
            };

        /// <summary>
        /// Adjusts the connection value based on the specified edge type.
        /// </summary>
        /// <param name="value">The input value to be adjusted.</param>
        /// <param name="valueType">The type of edge that determines how the value is adjusted.  Use <see cref="EdgeType.Binary"/> for binary
        /// adjustment, or other types for no modification.</param>
        /// <returns>The adjusted connection value. For <see cref="EdgeType.Binary"/>, returns 1 if <paramref name="value"/> is
        /// greater than 0; otherwise, 0.  For other edge types, returns the original <paramref name="value"/>.</returns>
        public static float FixConnectionValue(float value, EdgeType valueType)
            => valueType switch
            {
                EdgeType.Binary => value > 0 ? 1 : 0,
                _ => value
            };

        /// <summary>
        /// Converts/parses a 2d array of strings into a 2d array of floats with the provided top-left offset.
        /// Used by file importers.
        /// </summary>
        /// <param name="cells">A 2d array of strings.</param>
        /// <param name="startOffset">The top-left offset (use 1 to ignore the first row and column).</param>
        /// <returns>Returns a 2d array of parsed float values.</returns>
        public static float[,] ConvertStringCellsToFloatCells(string[,] cells, int startOffset)
        {
            int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            float[,] data = new float[nbrRows - startOffset, nbrCols - startOffset];
            for (int r = 0; r < nbrRows - startOffset; r++)
                for (int c = 0; c < nbrCols - startOffset; c++)
                    float.TryParse(cells[r + startOffset, c + startOffset], out data[r, c]);
            return data;
        }

        /// <summary>
        /// Converts a string of char-separated node ids into an array of these uint values.
        /// </summary>
        /// <param name="nodesString">A string with char-separated integer values.</param>
        /// <param name="sep">The separator character that should be used (default is semicolon ;)</param>
        /// <returns>Returns an array of unsigned integers.</returns>
        public static uint[]? SplitStringToUintArray(string nodesString, char sep = ';')
        {
            return nodesString.Split(sep).Select(s => uint.Parse(s)).ToArray() ?? null;
        }

        /// <summary>
        /// Convenience function for converting a boolean value to its lower-case textual representation.
        /// </summary>
        /// <param name="check"></param>
        /// <returns></returns>
        public static string BooleanAsString(bool check)
        {
            return check ? "true" : "false";
        }

        /// <summary>
        /// Method for sampling very large numbers from the geometric distribution, by using the
        /// inverse-CDF formula for a geometric random variable.
        /// </summary>
        /// <param name="p">The probability of success at each step.</param>
        /// <returns></returns>
        internal static ulong SampleGeometric(double p)
        {
            return (ulong)Math.Floor(Math.Log(Misc.Random.NextDouble()) / Math.Log(1.0 - p));
        }

        /// <summary>
        /// Generates the cumulative distribution function for the discrete Poisson distribution for the specified
        /// lambda and maxValue. Normalizes the probabilities so they sum up to 1.
        /// Used with the function for sampling from a given CDF.
        /// </summary>
        /// <param name="lambda">The lambda value (i.e. expected value)</param>
        /// <param name="maxValue">The max value that can be generated.</param>
        /// <returns>A double[] array of probabilities.</returns>
        internal static double[] BuildPoissonCDF(double lambda, int maxValue)
        {
            if (lambda < 0 || maxValue < 0)
                return [];
            double[] cdf = new double[maxValue + 1];
            double pmf = Math.Exp(-lambda);
            cdf[0] = pmf;
            for (int k = 1; k <= maxValue; k++)
            {
                pmf = pmf * lambda / k;
                cdf[k] = cdf[k - 1] + pmf;
            }
            double totalProb = cdf[maxValue];
            for (int k = 0; k <= maxValue; k++)
                cdf[k] /= totalProb;
            return cdf;
        }

        /// <summary>
        /// Sample from a precomputed cumulative distribution function (CDF).
        /// Uses binary search for speed. Returns the discrete random variable.
        /// </summary>
        /// <param name="cdf">The Cumulative distribution function, i.e. array of cumulative probabilities</param>
        /// <returns>Sampled integer value</returns>
        internal static int SampleFromCDF(double[] cdf)
        {
            double u = Random.NextDouble();
            int left = 0;
            int right = cdf.Length - 1;
            while (left < right)
            {
                int mid = left + (right - left) / 2;
                if (u <= cdf[mid])
                    right = mid;
                else left = mid + 1;
            }
            return left;
        }

        /// <summary>
        /// Partial Fisher-Yates for sampling k from an array: randomly reshuffling but only
        /// the first k entries in an array. Returns false if something went wrong.
        /// </summary>
        /// <typeparam name="T">Type of value that the array is about</typeparam>
        /// <param name="array">The array (by reference)</param>
        /// <param name="k">Number to randomize (i.e. k first)</param>
        /// <returns>True if successful, false if an error.</returns>
        internal static bool SampleWithoutReplacementInPlace<T>(T[] array, int k)
        {
            if (k > array.Length)
                return false;
            for (int i = 0; i < k; i++)
            {
                int j = Random.Next(i, array.Length);
                T temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
            return true;
        }

        /// <summary>
        /// Convenience function for expressing an edge with or without directional connotations.
        /// </summary>
        /// <param name="directionality">Directionality of the edge specified by a <see cref="EdgeDirectionality"/> value.</param>
        /// <param name="node1Id">The first node id.</param>
        /// <param name="node2Id">The second node id.</param>
        /// <returns>A string either expressing this as 'from [node1Id] to [node2Id]', or 'between [node1Id] and [node2Id]'.</returns>
        internal static string BetweenFromToText(EdgeDirectionality directionality, uint node1Id, uint node2Id)
        {
            return directionality == EdgeDirectionality.Directed ? $"from {node1Id} to {node2Id}" : $"between {node1Id} and {node2Id}";
        }

        internal static string GetFileEnding(string filepath)
        {
            var filename = Path.GetFileName(filepath);
            var firstDot = filename.IndexOf(".");
            return firstDot >= 0 ? filename.Substring(firstDot + 1) : "";
        }

        internal static FileFormat GetFileFormatFromFileEnding(string filepath)
        {
            return GetFileEnding(filepath) switch
            {
                "tsv" => FileFormat.Tsv,
                "tsv.gz" => FileFormat.TsvGzip,
                "bin" => FileFormat.Bin,
                "bin.gz" => FileFormat.BinGzip,
                _ => FileFormat.None
            };
        }

        /// <summary>
        /// Goes through a list and removes duplicates - also sorts it.
        /// Sorts list, then iterates through, rewriting and skipping duplicates in order
        /// </summary>
        /// <param name="list">The list with uint to be sorted and deduplicated</param>
        internal static void DeduplicateUintList(List<uint> list)
        {
            if (list.Count < 2)
                return;
            list.Sort();

            int write = 1;
            for (int read = 1; read < list.Count; read++)
            {
                uint v = list[read];
                if (v != list[read - 1])
                    list[write++] = v;
            }

            if (write < list.Count)
                list.RemoveRange(write, list.Count - write);
        }

        /// <summary>
        /// Deduplicates a list of Connection struct (on the basis of their partnerNodeId properties).
        /// Also sorts it by partnerNodeId.
        /// </summary>
        /// <param name="list">The list with Connection structs to be sorted and deduplicated by partnerNodeId</param>
        internal static void DeduplicateConnectionList(List<Connection> list)
        {
            if (list.Count < 2)
                return;
            list.Sort((a, b) => a.partnerNodeId.CompareTo(b.partnerNodeId));

            int write = 1;
            for (int read = 1; read < list.Count; read++)
            {
                uint v = list[read].partnerNodeId;
                if (v != list[read - 1].partnerNodeId)
                    list[write++] = list[read];
            }
            if (write < list.Count)
                list.RemoveRange(write, list.Count - write);
        }

        /// <summary>
        /// Takes a string and split it into a char[] array, using the optional separator
        /// character provided (defaults to semicolon). Returns null if something went wrong.
        /// </summary>
        /// <param name="charstring">The string consisting of semicolon-separated single characters.</param>
        /// <param name="sep">The separator character.</param>
        /// <returns>Returns a char array or null if something went wrong.</returns>
        internal static char[]? CharStringToChars(string charstring, char sep = ';')
        {
            if (string.IsNullOrWhiteSpace(charstring))
                return null;
            string[] parts = charstring.Split(sep);
            List<char> chars = [];
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (trimmed.Length != 1)
                    return null;
                chars.Add(trimmed[0]);
            }
            return chars.ToArray();
        }

        /// <summary>
        /// Method to trim quotes from a string, returning the trimmed string.
        /// Uses the constant array of quote characters as specified here as a Misc field.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        internal static string TrimQuotes(string text)
        {
            return text.Trim(_quoteChars);
        }

        #endregion
    }
}
