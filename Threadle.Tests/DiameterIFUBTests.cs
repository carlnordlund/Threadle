using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Cross-checks diameter(diameteronly=true) - the iFUB algorithm, started from a central root - against
/// the exhaustive all-pairs diameter on many graph shapes, 1-mode and 2-mode, and across several random
/// seeds (iFUB's start node is random, so every seed must give the same, exact result).
/// </summary>
public class DiameterIFUBTests
{
    private static readonly int[] Seeds = [1, 2, 3, 17, 42, 6031769];

    private static Network MakeNetwork(int nodeCount)
    {
        var nodeset = new Nodeset("ns");
        for (uint i = 1; i <= nodeCount; i++)
            nodeset.AddNode(i);
        return new Network("net", nodeset);
    }

    private static Network MakeUndirected(int nodeCount, IEnumerable<(uint, uint)> edges)
    {
        var net = MakeNetwork(nodeCount);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        foreach (var (a, b) in edges)
            net.AddEdge("layer", a, b);
        return net;
    }

    /// <summary>Asserts that iFUB gives the exhaustive diameter for every seed.</summary>
    private static void AssertIFUBMatchesExhaustive(Network net, string layer, EdgeTraversal traversal = EdgeTraversal.Out)
    {
        var exact = Analyses.Diameter(net, layer, traversal: traversal);
        Assert.True(exact.Success, exact.Message);
        int expected = (int)exact.Value!["Diameter"];
        foreach (int seed in Seeds)
        {
            Misc.SetRandomSeed(seed);
            var fast = Analyses.Diameter(net, layer, traversal: traversal, diameterOnly: true);
            Assert.True(fast.Success, fast.Message);
            Assert.Equal("iFUB", fast.Value!["Algorithm"]);
            Assert.Equal(expected, (int)fast.Value!["Diameter"]);
            Assert.Equal((int)exact.Value!["ComponentSize"], (int)fast.Value!["ComponentSize"]);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(12)]
    public void PathGraph_OddAndEvenDiameters(int n)
    {
        var edges = Enumerable.Range(1, n - 1).Select(i => ((uint)i, (uint)(i + 1)));
        var net = MakeUndirected(n, edges);
        AssertIFUBMatchesExhaustive(net, "layer");
        Assert.Equal(n - 1, (int)Analyses.Diameter(net, "layer", diameterOnly: true).Value!["Diameter"]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    public void CycleGraph(int n)
    {
        var edges = Enumerable.Range(1, n).Select(i => ((uint)i, (uint)(i % n + 1)));
        AssertIFUBMatchesExhaustive(MakeUndirected(n, edges), "layer");
    }

    [Fact]
    public void StarGraph()
    {
        var edges = Enumerable.Range(2, 9).Select(i => (1u, (uint)i));
        AssertIFUBMatchesExhaustive(MakeUndirected(10, edges), "layer");
    }

    [Fact]
    public void SingleEdge()
    {
        AssertIFUBMatchesExhaustive(MakeUndirected(2, [(1u, 2u)]), "layer");
    }

    [Fact]
    public void Lollipop_CliqueWithLongTail()
    {
        // A clique (nodes 1-6) with a 7-node tail: the diameter runs from the clique to the tail's end,
        // and a random start node may land in the clique or the tail.
        var edges = new List<(uint, uint)>();
        for (uint a = 1; a <= 6; a++)
            for (uint b = a + 1; b <= 6; b++)
                edges.Add((a, b));
        for (uint t = 6; t < 13; t++)
            edges.Add((t, t + 1));
        AssertIFUBMatchesExhaustive(MakeUndirected(13, edges), "layer");
    }

    [Fact]
    public void Tree_Branching()
    {
        // Binary-ish tree with uneven branch depths
        var edges = new List<(uint, uint)> { (1, 2), (1, 3), (2, 4), (2, 5), (4, 8), (8, 9), (9, 10), (3, 6), (3, 7), (7, 11), (11, 12) };
        AssertIFUBMatchesExhaustive(MakeUndirected(12, edges), "layer");
    }

    [Theory]
    [InlineData(40, 0.06)]
    [InlineData(80, 0.03)]
    [InlineData(150, 0.015)]
    public void ErdosRenyi_Sparse(int n, double p)
    {
        for (int g = 0; g < 4; g++)
        {
            Misc.SetRandomSeed(1000 + g);
            var net = MakeNetwork(n);
            net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
            Assert.True(Generators.GenerateErdosRenyiLayer(net, "layer", p).Success);
            AssertIFUBMatchesExhaustive(net, "layer");
        }
    }

    [Fact]
    public void WattsStrogatz_LowRewiring()
    {
        for (int g = 0; g < 3; g++)
        {
            Misc.SetRandomSeed(2000 + g);
            var net = MakeNetwork(120);
            net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
            Assert.True(Generators.GenerateWattsStrogatzLayer(net, "layer", 4, 0.05).Success);
            AssertIFUBMatchesExhaustive(net, "layer");
        }
    }

    [Fact]
    public void Directed_TraversalBoth()
    {
        // A directed path with a back-edge: under traversal=both it is treated as undirected.
        var net = MakeNetwork(8);
        net.AddLayerOneMode("layer", EdgeDirectionality.Directed, EdgeType.Binary, false);
        for (uint i = 1; i < 8; i++)
            net.AddEdge("layer", i, i + 1);
        net.AddEdge("layer", 5, 2);
        AssertIFUBMatchesExhaustive(net, "layer", EdgeTraversal.Both);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoMode_RandomAffiliations(bool packed)
    {
        for (int g = 0; g < 4; g++)
        {
            Misc.SetRandomSeed(3000 + g);
            var net = MakeNetwork(150);
            net.AddLayerTwoMode("aff");
            Assert.True(Generators.GenerateRandomTwoModeLayer(net, "aff", 60, 2).Success);
            if (packed)
                Assert.True(net.Pack("aff").Success);
            AssertIFUBMatchesExhaustive(net, "aff");
        }
    }

    [Fact]
    public void TwoMode_ChainOfHyperedges()
    {
        // Hyperedges chained by single shared members: h1={1,2,3}, h2={3,4,5}, h3={5,6,7}, h4={7,8}
        // gives node-to-node diameter 4 (1/2 to 8) - an elongated 2-mode shape, like a path.
        var net = MakeNetwork(8);
        net.AddLayerTwoMode("aff");
        net.AddHyperedge("aff", "h1", [1, 2, 3]);
        net.AddHyperedge("aff", "h2", [3, 4, 5]);
        net.AddHyperedge("aff", "h3", [5, 6, 7]);
        net.AddHyperedge("aff", "h4", [7, 8]);
        AssertIFUBMatchesExhaustive(net, "aff");
        Assert.Equal(4, (int)Analyses.Diameter(net, "aff", diameterOnly: true).Value!["Diameter"]);
    }
}