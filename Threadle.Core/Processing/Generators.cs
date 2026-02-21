using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Processing
{
    /// <summary>
    /// Various network generator methods.
    /// </summary>
    public static class NetworkGenerators
    {
        #region Methods (public)
        /// <summary>
        /// Generate an integer-type node attribute that is uniformly distributed between
        /// the two specified values, storing the value in the specified node attribute name.
        /// </summary>
        /// <param name="nodeset">The nodeset to create the node attribute in.</param>
        /// <param name="attrName">The name of the attribute.</param>
        /// <param name="minValue">Minimum integer value.</param>
        /// <param name="maxValue">Maximum integer value.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult GenerateIntAttr(Nodeset nodeset, string attrName, int minValue, int maxValue)
        {
            var attrDefineResult = nodeset.NodeAttributeDefinitionManager.DefineNewNodeAttribute(attrName, NodeAttributeType.Int);
            if (!attrDefineResult.Success)
                return attrDefineResult;
            byte attrIndex = attrDefineResult.Value;
            uint[] nodeIdArray = nodeset.NodeIdArray;
            for (int i = 0; i < nodeIdArray.Length; i++)
                nodeset.SetNodeAttribute(nodeIdArray[i], attrIndex, new NodeAttributeValue(Misc.Random.Next(minValue, maxValue + 1)));
            return OperationResult.Ok($"Node attribute '{attrName}' (integer) defined and random values between {minValue} and {maxValue} assigned to all nodes.");
        }

        /// <summary>
        /// Generate a float-type node attribute that is uniformly distributed between
        /// the two specified values, storing the value in the specified node attribute name.
        /// </summary>
        /// <param name="nodeset">The nodeset to create the node attribute in.</param>
        /// <param name="attrName">The name of the attribute.</param>
        /// <param name="minValue">Minimum float value.</param>
        /// <param name="maxValue">Maximum float value.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult GenerateFloatAttr(Nodeset nodeset, string attrName, float minValue, float maxValue)
        {
            var attrDefineResult = nodeset.NodeAttributeDefinitionManager.DefineNewNodeAttribute(attrName, NodeAttributeType.Float);
            if (!attrDefineResult.Success)
                return attrDefineResult;
            byte attrIndex = attrDefineResult.Value;
            uint[] nodeIdArray = nodeset.NodeIdArray;
            for (int i = 0; i < nodeIdArray.Length; i++)
                nodeset.SetNodeAttribute(nodeIdArray[i], attrIndex, new NodeAttributeValue(minValue + (float)(Misc.Random.NextDouble() * (maxValue - minValue))));
            return OperationResult.Ok($"Node attribute '{attrName}' (float) defined and random values between {minValue} and {maxValue} assigned to all nodes.");
        }

        /// <summary>
        /// Generate a random boolean node attribute with the probability p to be true.
        /// </summary>
        /// <param name="nodeset">The nodeset to create the node attribute in.</param>
        /// <param name="attrName">The name of the attribute.</param>
        /// <param name="p">The probability of the value being true.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult GenerateBoolAttr(Nodeset nodeset, string attrName, double p)
        {
            var attrDefineResult = nodeset.NodeAttributeDefinitionManager.DefineNewNodeAttribute(attrName, NodeAttributeType.Bool);
            if (!attrDefineResult.Success)
                return attrDefineResult;
            byte attrIndex = attrDefineResult.Value;
            uint[] nodeIdArray = nodeset.NodeIdArray;
            for (int i = 0; i < nodeIdArray.Length; i++)
                nodeset.SetNodeAttribute(nodeIdArray[i], attrIndex, new NodeAttributeValue(Misc.Random.NextDouble() < p));
            return OperationResult.Ok($"Node attribute '{attrName}' (bool) defined and true assigned to all nodes by probability {p}.");
        }

        /// <summary>
        /// Generate a char-type node attribute uniformly picked among the characters in the
        /// provided string.
        /// </summary>
        /// <param name="nodeset">The nodeset to create the node attribute in.</param>
        /// <param name="attrName">The name of the attribute.</param>
        /// <param name="charString">A string of semicolon-separated characters to pick from.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult GenerateCharAttr(Nodeset nodeset, string attrName, string charString)
        {
            var attrDefineResult = nodeset.NodeAttributeDefinitionManager.DefineNewNodeAttribute(attrName, NodeAttributeType.Char);
            if (!attrDefineResult.Success)
                return attrDefineResult;
            byte attrIndex = attrDefineResult.Value;

            char[]? chars = Misc.CharStringToChars(charString, ';');
            if (chars == null || chars.Length == 0)
                return OperationResult.Fail("InvalidArgument", $"The '{charString}' is either empty or does not contain a semicolon-separated series of individual characters.");
            int nbrChars = chars.Length;

            uint[] nodeIdArray = nodeset.NodeIdArray;

            for (int i = 0; i < nodeIdArray.Length; i++)
                nodeset.SetNodeAttribute(nodeIdArray[i], attrIndex, new NodeAttributeValue(chars[Misc.Random.Next(0, nbrChars)]));
            return OperationResult.Ok($"Node attribute '{attrName}' (char) defined and specified random characters assigned to all nodes.");
        }

        /// <summary>
        /// Generates random affiliation data in the specified network and 2-mode layer, with
        /// the specified number of hyperedges (affiliations) and the average number of affiliations
        /// each node should have. The number of affiliations is taken from the Poisson
        /// cumulative distribution function.
        /// </summary>
        /// <param name="network">The network in which to generate data.</param>
        /// <param name="layerName">The 2-mode layer to populate</param>
        /// <param name="h">The number of hyperedges to generate (automatically named aff_[0-(h-1)])</param>
        /// <param name="averageNbrAffiliations">Average number of affiliations each node should have.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult GenerateRandomTwoModeLayer(Network network, string layerName, int h, int averageNbrAffiliations)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            if (!(layerResult.Value is LayerTwoMode layer))
                return OperationResult.Fail("InvalidLayerMode", $"Layer '{layerName}' in network '{network.Name}' is not 2-mode.");

            if (h < 2)
                return OperationResult.Fail("InvalidArgument", "The number of hyperedges (h) must be at least 2.");
            if (averageNbrAffiliations < 1 || averageNbrAffiliations > h)
                return OperationResult.Fail("InvalidArgument", "The average number of node affiliations (a) must be at least 1 and no more than the number of hyperedges (h).");

            double[] cdf = Misc.BuildPoissonCDF(averageNbrAffiliations, h);

            Nodeset nodeset = network.Nodeset;

            uint[] nodeIds = nodeset.NodeIdArray;
            int n = nodeIds.Length;

            string[] hyperNames = new string[h];
            for (int j = 0; j < h; j++)
            {
                hyperNames[j] = $"aff_{j}";
                layer.AddHyperedge(hyperNames[j], null);
            }

            for (int i = 0; i < n; i++)
            {
                int nbrAffs = Misc.SampleFromCDF(cdf);
                Misc.SampleWithoutReplacementInPlace<string>(hyperNames, nbrAffs);
                for (int j = 0; j < nbrAffs; j++)
                    layer._addAffiliation(nodeIds[i], hyperNames[j]);
            }
            //return OperationResult.Ok();
            return OperationResult.Ok($"Randomized 2-mode network with h={h} hyperedges and a={averageNbrAffiliations} average number of affiliations per node generated in layer '{layerName}' in network '{network.Name}'.");
        }

        /// <summary>
        /// Generates a Barabasi-Albert network in the provided network and layer name. The layer must alread exist and be binary and symmetric.
        /// Any would-be existing ties in that layer will first be deleted.
        /// </summary>
        /// <param name="network">The network to generate the data in.</param>
        /// <param name="layerName">The layer to create the random network in (must be 1-mode, binary and symmetric)</param>
        /// <param name="m">The m parameter.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult GenerateBarabasiAlbertLayer(Network network, string layerName, int m)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            if (!(layerResult.Value is LayerOneMode layer))
                return OperationResult.Fail("InvalidLayerMode", $"Layer '{layerName}' in network '{network.Name}' is not 1-mode.");
            if (!layer.IsBinary)
                return OperationResult.Fail("InvalidLayerValueType", $"Layer '{layerName}' in network '{network.Name}' must be for binary edges.");
            if (!layer.IsSymmetric)
                return OperationResult.Fail("InvalidLayerEdgeDirection", $"Layer '{layerName}' in network '{network.Name}' must be for symmetric edges.");
            if (layer.Selfties)
                return OperationResult.Fail("ConstraintLayerAllowsSelfties", $"Layer '{layerName}' in network '{network.Name}' can't allow for selfties.");

            Nodeset nodeset = network.Nodeset;

            // Get an array of all node ids
            uint[] nodeIds = nodeset.NodeIdArray;
            int n = nodeIds.Length;

            if (m < 0 || m > n)
                return OperationResult.Fail("InvalidArgument", $"The attachment parameter (m) is {m}: it must be between 2 and the size of the network.");

            int totalEdges = (m * (m + 1)) / 2 + m * (n - m - 1);

            List<uint> edgeEndpoints = new List<uint>(2 * totalEdges);

            // Create initial clique with first m+1 nodes
            for (int i = 0; i <= m; i++)
            {
                for (int j = i + 1; j <= m; j++)
                {
                    layer._addEdge(nodeIds[i], nodeIds[j]);
                    edgeEndpoints.Add(nodeIds[i]);
                    edgeEndpoints.Add(nodeIds[j]);
                }
            }

            // Attach remaining nodes sequentially
            uint newNode, candidate;
            HashSet<uint> targets = new HashSet<uint>(m);
            for (int i = m + 1; i < n; i++)
            {
                newNode = nodeIds[i];
                targets.Clear();
                while (targets.Count < m)
                {
                    candidate = edgeEndpoints[Misc.Random.Next(edgeEndpoints.Count)];
                    if (candidate != newNode)
                        targets.Add(candidate);
                }
                foreach (uint target in targets)
                {
                    layer._addEdge(newNode, target);
                    edgeEndpoints.Add(newNode);
                    edgeEndpoints.Add(target);
                }
            }
            return OperationResult.Ok($"Barabasi-Albert network with m={m} generated in layer '{layerName}' in network '{network.Name}'.");
        }

        /// <summary>
        /// Generate a Watts-Strogatz small-world-syle random network in an existing 1-mode layer,
        /// that is binary and symmetric. It works by first generating a ring lattice (with the
        /// specified degree k), and then rewiring edges with the beta probability.
        /// The k must be an even number: it will then create edges with the k/2 preceding node ids
        /// and with the k/2 following node ids.
        /// </summary>
        /// <param name="network">The network to generate the data in.</param>
        /// <param name="layerName">The 1-mode binary and symmetric layer to create the WS network in.</param>
        /// <param name="k">The k parameter (node degree) - must be an even number!</param>
        /// <param name="beta">The probability of rewiring a forward-looking edge.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult GenerateWattsStrogatzLayer(Network network, string layerName, int k, double beta)
        {
            if (k % 2 != 0)
                return OperationResult.Fail("InvalidArgument", $"The node degree (k) is {k}; it must be an even number.");
            if (beta < 0 || beta > 1)
                return OperationResult.Fail("InvalidArgument", $"The rewiring probability (beta) is {beta}; it must be between 0 and 1.");
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            if (!(layerResult.Value is LayerOneMode layer))
                return OperationResult.Fail("InvalidLayerMode", $"Layer '{layerName}' in network '{network.Name}' is not 1-mode.");
            if (!layer.IsBinary)
                return OperationResult.Fail("InvalidLayerValueType", $"Layer '{layerName}' in network '{network.Name}' must be for binary edges.");
            if (!layer.IsSymmetric)
                return OperationResult.Fail("InvalidLayerEdgeDirection", $"Layer '{layerName}' in network '{network.Name}' must be for symmetric edges.");
            if (layer.Selfties)
                return OperationResult.Fail("ConstraintLayerAllowsSelfties", $"Layer '{layerName}' in network '{network.Name}' can't allow for selfties.");

            layer.ClearLayer();

            Nodeset nodeset = network.Nodeset;

            // Get an array of all node ids
            uint[] nodeIds = nodeset.NodeIdArray;
            uint n = (uint)nodeIds.Length;

            // Create ring lattice
            for (int i = 0; i < n; i++)
                for (int j = 1; j <= k / 2; j++)
                    layer._addEdge(nodeIds[i], nodeIds[(i + j) % n]);

            // Then: rewiring!
            uint source, oldTarget, newTarget;
            for (int i = 0; i < n; i++)
            {
                source = nodeIds[i];
                // For rewiring: check the forward-oriented edges (will wrap around the edge)
                for (int j = 1; j <= k / 2; j++)
                {
                    if (Misc.Random.NextDouble() < beta)
                    {
                        // Note how modulus by size of nodeset will make the wrap!
                        oldTarget = nodeIds[(i + j) % n];
                        do
                        {
                            newTarget = nodeIds[Misc.Random.Next(0, nodeIds.Length - 1)];

                        }
                        while (newTarget == i || layer.CheckEdgeExists(source, newTarget));
                        layer.RemoveEdge(source, oldTarget);
                        layer.AddEdge(source, newTarget);
                    }
                }
            }

            return OperationResult.Ok($"Watts-Strogatz network with k={k} and beta={beta} generated in layer '{layerName}' in network '{network.Name}'.");
        }

        /// <summary>
        /// Generates an Erdös-Renyi network in the provided network and layer name. The layer must alread exist and be binary.
        /// Any would-be existing ties in that layer will first be deleted. The generator will then follow the properties of the
        /// layer, i.e. whether directional or symmetric, whether selfties or not.
        /// This approach utilizes the very clever solution proposed by Batagelj and Brandes (2005)
        /// in https://doi.org/10.1103/PhysRevE.71.036113
        /// </summary>
        /// <param name="network">The network to generate this data in,</param>
        /// <param name="layerName">The name of the layer to create it in.</param>
        /// <param name="p">The tie probability.</param>
        /// <returns></returns>
        public static OperationResult GenerateErdosRenyiLayer(Network network, string layerName, double p)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            if (!(layerResult.Value is LayerOneMode layer))
                return OperationResult.Fail("InvalidLayerMode", $"Layer '{layerName}' in network '{network.Name}' is not 1-mode.");
            if (!layer.IsBinary)
                return OperationResult.Fail("InvalidLayerValueType", $"Layer '{layerName}' in network '{network.Name}' is not for binary edges.");

            // Clear the layer
            layer.ClearLayer();

            // Get reference to nodeset
            Nodeset nodeset = network.Nodeset;
            uint n = (uint)nodeset.Count;

            // Determine the expected degree (i.e. number of directed connections per node)
            int edgesetCapacity = (int)(p * n);

            // Get an array of all node ids
            uint[] nodeIds = nodeset.NodeIdArray;

            // Initialize the capacity of the Edgesets dictionary as well as the number of connections for each IEdgeset
            layer._initEdgesets(nodeIds, edgesetCapacity);

            ulong totalEdges = Misc.GetNbrPotentialEdges(n, layer.Directionality, layer.Selfties);
            ulong index = 0;
            uint row = 0;
            ulong rowStartindex = 0;
            uint rowLength = GetRowLength(row, n, layer.Directionality, layer.Selfties);
            while (index < totalEdges)
            {
                ulong skip = Misc.SampleGeometric(p);
                index += skip;
                if (index >= totalEdges)
                    break;
                while (index >= rowStartindex + rowLength)
                {
                    rowStartindex += rowLength;
                    row++;
                    rowLength = GetRowLength(row, n, layer.Directionality, layer.Selfties);
                }
                uint offsetInRow = (uint)(index - rowStartindex);
                uint col = GetCol(row, offsetInRow, n, layer.Directionality, layer.Selfties);

                // Bypass validation when creating edges
                layer.Edgesets[nodeIds[row]]._addOutboundEdge(nodeIds[col], 1);
                layer.Edgesets[nodeIds[col]]._addInboundEdge(nodeIds[row], 1);
                index++;
            }
            return OperationResult.Ok($"Erdös-Renyi network with p={p} generated in layer '{layerName}' in network '{network.Name}'.");
        }
        #endregion


        #region Methods (private)
        /// <summary>
        /// Calculates the number of elements in a row based on the specified parameters.
        /// </summary>
        /// <param name="row">The zero-based index of the current row.</param>
        /// <param name="n">The total number of elements in the structure.</param>
        /// <param name="directionality">The directionality of the edges, indicating whether they are directed or undirected.</param>
        /// <param name="selfties">A value indicating whether self-ties (connections to the same element) are allowed.</param>
        /// <returns>The number of elements in the specified row, adjusted based on the directionality and self-ties settings.</returns>
        private static uint GetRowLength(uint row, uint n, EdgeDirectionality directionality, bool selfties)
        {
            if (directionality == EdgeDirectionality.Directed)
                return selfties ? n : n - 1;
            else
                return selfties ? n - row : n - row - 1;
        }

        /// <summary>
        /// Calculates the column index in a graph based on the specified row, offset, graph size, edge directionality,
        /// and whether self-loops are allowed.
        /// </summary>
        /// <param name="row">The zero-based index of the row in the graph.</param>
        /// <param name="offsetInRow">The zero-based offset within the specified row.</param>
        /// <param name="n">The total number of nodeIds in the graph. This parameter is not used in the calculation.</param>
        /// <param name="directionality">Specifies whether the graph is directed or undirected. If directed, the calculation accounts for the
        /// direction of edges.</param>
        /// <param name="selfties">A value indicating whether self-loops (edges from a node to itself) are allowed. If <see langword="true"/>,
        /// self-loops are included in the calculation.</param>
        /// <returns>The calculated column index as a zero-based unsigned integer.</returns>
        private static uint GetCol(uint row, uint offsetInRow, long n, EdgeDirectionality directionality, bool selfties)
        {
            if (directionality == EdgeDirectionality.Directed)
            {
                if (selfties)
                    return offsetInRow;
                else
                    return offsetInRow >= row ? offsetInRow + 1 : offsetInRow;
            }
            else
                return (uint)(row + offsetInRow + (selfties ? 0 : 1));
        }
        #endregion
    }
}
