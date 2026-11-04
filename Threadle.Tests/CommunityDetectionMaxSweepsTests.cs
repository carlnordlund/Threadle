using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Tests for communitydetection()'s 'maxsweeps' option (cap on local-moving sweeps per level) for
/// louvain, leiden, lpam and plm, on 1-mode and 2-mode layers: the default (0) and a cap that is never
/// reached both reproduce the uncapped result exactly; a small cap still gives a valid partition and
/// still recovers clear planted communities; a negative value is rejected.
/// </summary>
[Collection("MaxThreadsLimit")] // plm cases set maxthreads: run apart from all other tests
public class CommunityDetectionMaxSweepsTests : IDisposable
{
    public void Dispose() => UserSettings.MaxDegreeOfParallelism = -1;

    public enum Method { Louvain, Leiden, LPAm, PLM }

    private static OperationResult<Dictionary<string, object>> Run(Method m, Network net, string layer, string attr, int? maxSweeps)
    {
        string[] layers = [layer];
        return m switch
        {
            Method.Louvain => maxSweeps is int a ? Analyses.CommunityDetectionLouvain(net, layers, attr, maxSweeps: a) : Analyses.CommunityDetectionLouvain(net, layers, attr),
            Method.Leiden => maxSweeps is int b ? Analyses.CommunityDetectionLeiden(net, layers, attr, maxSweeps: b) : Analyses.CommunityDetectionLeiden(net, layers, attr),
            Method.LPAm => maxSweeps is int c ? Analyses.CommunityDetectionLPAm(net, layers, attr, maxSweeps: c) : Analyses.CommunityDetectionLPAm(net, layers, attr),
            _ => maxSweeps is int d ? Analyses.CommunityDetectionPLM(net, layers, attr, maxSweeps: d) : Analyses.CommunityDetectionPLM(net, layers, attr),
        };
    }

    private static int Community(Network net, uint node, string attr)
    {
        var r = net.Nodeset.GetNodeAttribute(node, attr);
        Assert.True(r.Success, r.Message);
        return (int)r.Value.Value.GetValue(r.Value.Type)!;
    }

    /// <summary>A random network (scale-free 1-mode, or random 2-mode), generated reproducibly.</summary>
    private static Network MakeRandom(bool twoMode)
    {
        Misc.SetRandomSeed(21);
        var ns = new Nodeset("ns");
        for (uint i = 0; i < 1500; i++)
            ns.AddNode(i);
        var net = new Network("net", ns);
        if (twoMode)
        {
            net.AddLayerTwoMode("x");
            Assert.True(Generators.GenerateRandomTwoModeLayer(net, "x", 500, 2).Success);
            Assert.True(net.Pack("x").Success);
        }
        else
        {
            net.AddLayerOneMode("x", EdgeDirectionality.Undirected, EdgeType.Binary, false);
            Assert.True(Generators.GenerateBarabasiAlbertLayer(net, "x", 3).Success);
        }
        return net;
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (Method m in Enum.GetValues<Method>())
        {
            yield return [m, false];
            yield return [m, true];
        }
    }

    private static void AssertSameResult(Network net, string attrA, string attrB, OperationResult<Dictionary<string, object>> a, OperationResult<Dictionary<string, object>> b)
    {
        Assert.True(a.Success, a.Message);
        Assert.True(b.Success, b.Message);
        Assert.Equal((double)a.Value!["Modularity"], (double)b.Value!["Modularity"]);
        foreach (uint id in net.Nodeset.NodeIdArray)
            Assert.Equal(Community(net, id, attrA), Community(net, id, attrB));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ZeroAndUnreachedCap_MatchDefault(Method m, bool twoMode)
    {
        UserSettings.MaxDegreeOfParallelism = 1; // plm is only reproducible single-threaded
        var net = MakeRandom(twoMode);
        Misc.SetRandomSeed(5);
        var dflt = Run(m, net, "x", "dflt", null);
        Misc.SetRandomSeed(5);
        var zero = Run(m, net, "x", "zero", 0);
        AssertSameResult(net, "dflt", "zero", dflt, zero);

        // plm's default is its own cap of 32, so a "never reached" cap only matches the default for the others
        if (m != Method.PLM)
        {
            Misc.SetRandomSeed(5);
            var large = Run(m, net, "x", "large", 100_000);
            AssertSameResult(net, "dflt", "large", dflt, large);
        }
        else
        {
            Misc.SetRandomSeed(5);
            var cap32 = Run(m, net, "x", "cap32", 32);
            AssertSameResult(net, "dflt", "cap32", dflt, cap32);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SmallCap_GivesValidPartition(Method m, bool twoMode)
    {
        UserSettings.MaxDegreeOfParallelism = 2;
        var net = MakeRandom(twoMode);
        var r = Run(m, net, "x", "c", 1);
        Assert.True(r.Success, r.Message);
        var sizes = ((IEnumerable<int>)r.Value!["CommunitySizes"]).ToList();
        Assert.Equal(net.Nodeset.Count, sizes.Sum());
        Assert.Equal(sizes.Count, net.Nodeset.NodeIdArray.Select(id => Community(net, id, "c")).Distinct().Count());
        Assert.True((double)r.Value!["Modularity"] > 0);
    }

    [Theory]
    [InlineData(Method.Louvain)]
    [InlineData(Method.Leiden)]
    [InlineData(Method.LPAm)]
    [InlineData(Method.PLM)]
    public void Cap_StillRecoversCliques(Method m)
    {
        UserSettings.MaxDegreeOfParallelism = 2;
        var ns = new Nodeset("ns");
        for (uint i = 0; i < 60; i++)
            ns.AddNode(i);
        var net = new Network("net", ns);
        net.AddLayerOneMode("x", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        for (uint g = 0; g < 6; g++)
        {
            uint b = g * 10;
            for (uint a = 0; a < 10; a++)
                for (uint c = a + 1; c < 10; c++)
                    net.AddEdge("x", b + a, b + c);
            if (g > 0)
                net.AddEdge("x", b - 1, b);
        }
        var r = Run(m, net, "x", "c", 3);
        Assert.True(r.Success, r.Message);
        Assert.Equal(6, (int)r.Value!["NbrCommunities"]);
        for (uint g = 0; g < 6; g++)
            for (uint a = 1; a < 10; a++)
                Assert.Equal(Community(net, g * 10, "c"), Community(net, g * 10 + a, "c"));
    }

    [Theory]
    [InlineData(Method.Louvain)]
    [InlineData(Method.Leiden)]
    [InlineData(Method.LPAm)]
    [InlineData(Method.PLM)]
    public void NegativeCap_IsRejected(Method m)
    {
        var net = MakeRandom(false);
        var r = Run(m, net, "x", "c", -1);
        Assert.False(r.Success);
        Assert.Equal("InvalidArgument", r.Code);
    }
}