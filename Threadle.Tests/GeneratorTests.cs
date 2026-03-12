using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;

namespace Threadle.Tests;

public class GeneratorTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Nodeset MakeNodeset(int count = 20) => new Nodeset("ns", count);

    private static Network MakeNetworkWithUndirectedLayer(int nodeCount = 20, string layerName = "layer")
    {
        var nodeset = MakeNodeset(nodeCount);
        var network = new Network("net", nodeset);
        network.AddLayerOneMode(layerName, EdgeDirectionality.Undirected, EdgeType.Binary, false);
        return network;
    }

    private static Network MakeNetworkWithDirectedLayer(int nodeCount = 20, string layerName = "layer")
    {
        var nodeset = MakeNodeset(nodeCount);
        var network = new Network("net", nodeset);
        network.AddLayerOneMode(layerName, EdgeDirectionality.Directed, EdgeType.Binary, false);
        return network;
    }

    private static Network MakeNetworkWithTwoModeLayer(int nodeCount = 20, string layerName = "layer")
    {
        var nodeset = MakeNodeset(nodeCount);
        var network = new Network("net", nodeset);
        network.AddLayerTwoMode(layerName);
        return network;
    }

    // ── GenerateIntAttr ──────────────────────────────────────────────────────

    [Fact]
    public void GenerateIntAttr_ValidArgs_Succeeds()
    {
        var nodeset = MakeNodeset();
        var result = Generators.GenerateIntAttr(nodeset, "age", 0, 100);
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateIntAttr_DefinesAttribute()
    {
        var nodeset = MakeNodeset(5);
        Generators.GenerateIntAttr(nodeset, "age", 0, 100);
        var get = nodeset.GetNodeAttribute(nodeset.NodeIdArray[0], "age");
        Assert.True(get.Success);
    }

    [Fact]
    public void GenerateIntAttr_AllValuesWithinRange()
    {
        var nodeset = MakeNodeset(50);
        Generators.GenerateIntAttr(nodeset, "age", 10, 20);
        foreach (uint id in nodeset.NodeIdArray)
        {
            var (nav, type) = nodeset.GetNodeAttribute(id, "age").Value;
            int v = (int)nav.GetValue(type);
            Assert.InRange(v, 10, 20);
        }
    }

    [Fact]
    public void GenerateIntAttr_MinEqualsMax_AllValuesSame()
    {
        var nodeset = MakeNodeset(10);
        Generators.GenerateIntAttr(nodeset, "score", 42, 42);
        foreach (uint id in nodeset.NodeIdArray)
        {
            var (nav, type) = nodeset.GetNodeAttribute(id, "score").Value;
            Assert.Equal(42, (int)nav.GetValue(type));
        }
    }

    [Fact]
    public void GenerateIntAttr_DuplicateAttributeName_Fails()
    {
        var nodeset = MakeNodeset();
        Generators.GenerateIntAttr(nodeset, "age", 0, 100);
        var result = Generators.GenerateIntAttr(nodeset, "age", 0, 100);
        Assert.False(result.Success);
    }

    [Fact]
    public void GenerateIntAttr_EmptyNodeset_Succeeds()
    {
        var nodeset = MakeNodeset(0);
        var result = Generators.GenerateIntAttr(nodeset, "age", 0, 100);
        Assert.True(result.Success);
    }

    // ── GenerateFloatAttr ────────────────────────────────────────────────────

    [Fact]
    public void GenerateFloatAttr_ValidArgs_Succeeds()
    {
        var nodeset = MakeNodeset();
        var result = Generators.GenerateFloatAttr(nodeset, "score", 0f, 1f);
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateFloatAttr_AllValuesWithinRange()
    {
        var nodeset = MakeNodeset(50);
        Generators.GenerateFloatAttr(nodeset, "score", 1.0f, 5.0f);
        foreach (uint id in nodeset.NodeIdArray)
        {
            var (nav, type) = nodeset.GetNodeAttribute(id, "score").Value;
            float v = (float)nav.GetValue(type);
            Assert.InRange(v, 1.0f, 5.0f);
        }
    }

    [Fact]
    public void GenerateFloatAttr_DefinesAttribute()
    {
        var nodeset = MakeNodeset(5);
        Generators.GenerateFloatAttr(nodeset, "score", 0f, 1f);
        var get = nodeset.GetNodeAttribute(nodeset.NodeIdArray[0], "score");
        Assert.True(get.Success);
    }

    [Fact]
    public void GenerateFloatAttr_DuplicateAttributeName_Fails()
    {
        var nodeset = MakeNodeset();
        Generators.GenerateFloatAttr(nodeset, "score", 0f, 1f);
        var result = Generators.GenerateFloatAttr(nodeset, "score", 0f, 1f);
        Assert.False(result.Success);
    }

    // ── GenerateBoolAttr ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateBoolAttr_ValidArgs_Succeeds()
    {
        var nodeset = MakeNodeset();
        var result = Generators.GenerateBoolAttr(nodeset, "active", 0.5);
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateBoolAttr_ProbabilityZero_AllFalse()
    {
        var nodeset = MakeNodeset(20);
        Generators.GenerateBoolAttr(nodeset, "active", 0.0);
        foreach (uint id in nodeset.NodeIdArray)
        {
            var (nav, type) = nodeset.GetNodeAttribute(id, "active").Value;
            Assert.False((bool)nav.GetValue(type));
        }
    }

    [Fact]
    public void GenerateBoolAttr_ProbabilityOne_AllTrue()
    {
        var nodeset = MakeNodeset(20);
        Generators.GenerateBoolAttr(nodeset, "active", 1.0);
        foreach (uint id in nodeset.NodeIdArray)
        {
            var (nav, type) = nodeset.GetNodeAttribute(id, "active").Value;
            Assert.True((bool)nav.GetValue(type));
        }
    }

    [Fact]
    public void GenerateBoolAttr_DefinesAttribute()
    {
        var nodeset = MakeNodeset(5);
        Generators.GenerateBoolAttr(nodeset, "active", 0.5);
        var get = nodeset.GetNodeAttribute(nodeset.NodeIdArray[0], "active");
        Assert.True(get.Success);
    }

    [Fact]
    public void GenerateBoolAttr_DuplicateAttributeName_Fails()
    {
        var nodeset = MakeNodeset();
        Generators.GenerateBoolAttr(nodeset, "active", 0.5);
        var result = Generators.GenerateBoolAttr(nodeset, "active", 0.5);
        Assert.False(result.Success);
    }

    // ── GenerateCharAttr ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateCharAttr_ValidArgs_Succeeds()
    {
        var nodeset = MakeNodeset();
        var result = Generators.GenerateCharAttr(nodeset, "gender", "m;f");
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateCharAttr_AllValuesFromCharString()
    {
        var nodeset = MakeNodeset(50);
        Generators.GenerateCharAttr(nodeset, "gender", "m;f");
        foreach (uint id in nodeset.NodeIdArray)
        {
            var (nav, type) = nodeset.GetNodeAttribute(id, "gender").Value;
            char v = (char)nav.GetValue(type);
            Assert.Contains(v, new[] { 'm', 'f' });
        }
    }

    [Fact]
    public void GenerateCharAttr_SingleOption_AllValuesSame()
    {
        var nodeset = MakeNodeset(10);
        Generators.GenerateCharAttr(nodeset, "category", "x");
        foreach (uint id in nodeset.NodeIdArray)
        {
            var (nav, type) = nodeset.GetNodeAttribute(id, "category").Value;
            Assert.Equal('x', (char)nav.GetValue(type));
        }
    }

    [Fact]
    public void GenerateCharAttr_EmptyCharString_Fails()
    {
        var nodeset = MakeNodeset();
        var result = Generators.GenerateCharAttr(nodeset, "gender", "");
        Assert.False(result.Success);
    }

    [Fact]
    public void GenerateCharAttr_DuplicateAttributeName_Fails()
    {
        var nodeset = MakeNodeset();
        Generators.GenerateCharAttr(nodeset, "gender", "m;f");
        var result = Generators.GenerateCharAttr(nodeset, "gender", "m;f");
        Assert.False(result.Success);
    }

    [Fact]
    public void GenerateCharAttr_DefinesAttribute()
    {
        var nodeset = MakeNodeset(5);
        Generators.GenerateCharAttr(nodeset, "gender", "m;f");
        var get = nodeset.GetNodeAttribute(nodeset.NodeIdArray[0], "gender");
        Assert.True(get.Success);
    }

    // ── GenerateErdosRenyiLayer ──────────────────────────────────────────────

    [Fact]
    public void GenerateErdosRenyiLayer_ValidArgs_Succeeds()
    {
        var network = MakeNetworkWithUndirectedLayer();
        var result = Generators.GenerateErdosRenyiLayer(network, "layer", 0.5);
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateErdosRenyiLayer_PZero_NoEdges()
    {
        var network = MakeNetworkWithUndirectedLayer();
        Generators.GenerateErdosRenyiLayer(network, "layer", 0.0);
        var edges = network.GetAllEdges("layer");
        Assert.Equal(0, edges.Value!.Count);
    }

    [Fact]
    public void GenerateErdosRenyiLayer_POne_MaxEdges()
    {
        var network = MakeNetworkWithUndirectedLayer(10);
        Generators.GenerateErdosRenyiLayer(network, "layer", 1.0);
        var edges = network.GetAllEdges("layer");
        // Undirected, no selfties: n*(n-1)/2 = 10*9/2 = 45
        Assert.Equal(45, edges.Value!.Count);
    }

    [Fact]
    public void GenerateErdosRenyiLayer_NegativeP_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer();
        var result = Generators.GenerateErdosRenyiLayer(network, "layer", -0.1);
        Assert.False(result.Success);
        Assert.Equal("InvalidParameter", result.Code);
    }

    [Fact]
    public void GenerateErdosRenyiLayer_PGreaterThanOne_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer();
        var result = Generators.GenerateErdosRenyiLayer(network, "layer", 1.1);
        Assert.False(result.Success);
        Assert.Equal("InvalidParameter", result.Code);
    }

    [Fact]
    public void GenerateErdosRenyiLayer_NonExistentLayer_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer();
        var result = Generators.GenerateErdosRenyiLayer(network, "ghost", 0.5);
        Assert.False(result.Success);
    }

    [Fact]
    public void GenerateErdosRenyiLayer_TwoModeLayer_Fails()
    {
        var network = MakeNetworkWithTwoModeLayer();
        var result = Generators.GenerateErdosRenyiLayer(network, "layer", 0.5);
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerMode", result.Code);
    }

    [Fact]
    public void GenerateErdosRenyiLayer_DirectedLayer_Succeeds()
    {
        var network = MakeNetworkWithDirectedLayer();
        var result = Generators.GenerateErdosRenyiLayer(network, "layer", 0.5);
        Assert.True(result.Success);
    }

    // ── GenerateWattsStrogatzLayer ───────────────────────────────────────────

    [Fact]
    public void GenerateWattsStrogatzLayer_ValidArgs_Succeeds()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        var result = Generators.GenerateWattsStrogatzLayer(network, "layer", 4, 0.1);
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateWattsStrogatzLayer_OddK_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        var result = Generators.GenerateWattsStrogatzLayer(network, "layer", 3, 0.1);
        Assert.False(result.Success);
        Assert.Equal("InvalidArgument", result.Code);
    }

    [Fact]
    public void GenerateWattsStrogatzLayer_NegativeBeta_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        var result = Generators.GenerateWattsStrogatzLayer(network, "layer", 4, -0.1);
        Assert.False(result.Success);
        Assert.Equal("InvalidArgument", result.Code);
    }

    [Fact]
    public void GenerateWattsStrogatzLayer_BetaGreaterThanOne_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        var result = Generators.GenerateWattsStrogatzLayer(network, "layer", 4, 1.1);
        Assert.False(result.Success);
        Assert.Equal("InvalidArgument", result.Code);
    }

    [Fact]
    public void GenerateWattsStrogatzLayer_NonExistentLayer_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        var result = Generators.GenerateWattsStrogatzLayer(network, "ghost", 4, 0.1);
        Assert.False(result.Success);
    }

    [Fact]
    public void GenerateWattsStrogatzLayer_DirectedLayer_Fails()
    {
        var network = MakeNetworkWithDirectedLayer(20);
        var result = Generators.GenerateWattsStrogatzLayer(network, "layer", 4, 0.1);
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerEdgeDirection", result.Code);
    }

    [Fact]
    public void GenerateWattsStrogatzLayer_BetaZero_ProducesRingLattice()
    {
        // Beta=0 means no rewiring: each node has exactly k edges
        var network = MakeNetworkWithUndirectedLayer(20);
        Generators.GenerateWattsStrogatzLayer(network, "layer", 4, 0.0);
        var edges = network.GetAllEdges("layer");
        // Ring lattice: n * k/2 = 20 * 2 = 40 edges
        Assert.Equal(40, edges.Value!.Count);
    }

    // ── GenerateBarabasiAlbertLayer ──────────────────────────────────────────

    [Fact]
    public void GenerateBarabasiAlbertLayer_ValidArgs_Succeeds()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        var result = Generators.GenerateBarabasiAlbertLayer(network, "layer", 2);
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateBarabasiAlbertLayer_NonExistentLayer_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        var result = Generators.GenerateBarabasiAlbertLayer(network, "ghost", 2);
        Assert.False(result.Success);
    }

    [Fact]
    public void GenerateBarabasiAlbertLayer_DirectedLayer_Fails()
    {
        var network = MakeNetworkWithDirectedLayer(20);
        var result = Generators.GenerateBarabasiAlbertLayer(network, "layer", 2);
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerEdgeDirection", result.Code);
    }

    [Fact]
    public void GenerateBarabasiAlbertLayer_MGreaterThanN_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer(10);
        var result = Generators.GenerateBarabasiAlbertLayer(network, "layer", 11);
        Assert.False(result.Success);
        Assert.Equal("InvalidArgument", result.Code);
    }

    [Fact]
    public void GenerateBarabasiAlbertLayer_TwoModeLayer_Fails()
    {
        var network = MakeNetworkWithTwoModeLayer(20);
        var result = Generators.GenerateBarabasiAlbertLayer(network, "layer", 2);
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerMode", result.Code);
    }

    [Fact]
    public void GenerateBarabasiAlbertLayer_ProducesEdges()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        Generators.GenerateBarabasiAlbertLayer(network, "layer", 2);
        var edges = network.GetAllEdges("layer");
        Assert.True(edges.Value!.Count > 0);
    }

    // ── GenerateRandomTwoModeLayer ───────────────────────────────────────────

    [Fact]
    public void GenerateRandomTwoModeLayer_ValidArgs_Succeeds()
    {
        var network = MakeNetworkWithTwoModeLayer(20);
        var result = Generators.GenerateRandomTwoModeLayer(network, "layer", 5, 2);
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateRandomTwoModeLayer_HTooSmall_Fails()
    {
        var network = MakeNetworkWithTwoModeLayer(20);
        var result = Generators.GenerateRandomTwoModeLayer(network, "layer", 1, 1);
        Assert.False(result.Success);
        Assert.Equal("InvalidArgument", result.Code);
    }

    [Fact]
    public void GenerateRandomTwoModeLayer_AffiliationsLessThanOne_Fails()
    {
        var network = MakeNetworkWithTwoModeLayer(20);
        var result = Generators.GenerateRandomTwoModeLayer(network, "layer", 5, 0);
        Assert.False(result.Success);
        Assert.Equal("InvalidArgument", result.Code);
    }

    [Fact]
    public void GenerateRandomTwoModeLayer_AffiliationsGreaterThanH_Fails()
    {
        var network = MakeNetworkWithTwoModeLayer(20);
        var result = Generators.GenerateRandomTwoModeLayer(network, "layer", 5, 6);
        Assert.False(result.Success);
        Assert.Equal("InvalidArgument", result.Code);
    }

    [Fact]
    public void GenerateRandomTwoModeLayer_NonExistentLayer_Fails()
    {
        var network = MakeNetworkWithTwoModeLayer(20);
        var result = Generators.GenerateRandomTwoModeLayer(network, "ghost", 5, 2);
        Assert.False(result.Success);
    }

    [Fact]
    public void GenerateRandomTwoModeLayer_OneModeLayer_Fails()
    {
        var network = MakeNetworkWithUndirectedLayer(20);
        var result = Generators.GenerateRandomTwoModeLayer(network, "layer", 5, 2);
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerMode", result.Code);
    }

    [Fact]
    public void GenerateRandomTwoModeLayer_CreatesHyperedges()
    {
        var network = MakeNetworkWithTwoModeLayer(20);
        Generators.GenerateRandomTwoModeLayer(network, "layer", 5, 2);
        var hyperedges = network.GetAllHyperedges("layer");
        Assert.Equal(5, hyperedges.Value!.Length);
    }
}
