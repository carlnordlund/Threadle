using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Tests;

/// <summary>
/// Categorical assortativity on a 2-mode layer against an independent reference written here: Newman's
/// nominal r on the mixing counts of co-member pairs, each unordered pair of categorized nodes counted
/// once per shared hyperedge ('count') or once if it shares any ('binary'). Count mode uses a
/// per-hyperedge closed form in Threadle; this checks it gives the same value as pair enumeration, on
/// unpacked and packed layers, with uncategorized members and hyperedges of very different sizes.
/// </summary>
public class AssortativityTwoModeReferenceTests
{
    private static double Reference(Network net, string layer, Dictionary<uint, string> cats, bool binary)
    {
        // Pair weights: number of shared hyperedges (count) or 1 (binary)
        var pairs = new Dictionary<(uint, uint), int>();
        foreach (var (_, members) in ((ILayerTwoMode)net.Layers[layer]).GetAllHyperedgeData())
        {
            var m = members.Where(cats.ContainsKey).Distinct().OrderBy(x => x).ToArray();
            for (int i = 0; i < m.Length; i++)
                for (int j = i + 1; j < m.Length; j++)
                    pairs[(m[i], m[j])] = pairs.GetValueOrDefault((m[i], m[j])) + 1;
        }
        double M = 0, match = 0;
        var ends = new Dictionary<string, double>();
        foreach (var ((u, v), c) in pairs)
        {
            double w = binary ? 1 : c;
            M += w;
            ends[cats[u]] = ends.GetValueOrDefault(cats[u]) + w;
            ends[cats[v]] = ends.GetValueOrDefault(cats[v]) + w;
            if (cats[u] == cats[v]) match += w;
        }
        if (M == 0) return 0;
        double a2 = ends.Values.Sum(x => Math.Pow(x / (2 * M), 2));
        return (match / M - a2) / (1 - a2);
    }

    private static (Network Net, Dictionary<uint, string> Cats) Make(int seed, bool packed)
    {
        var rng = new Random(seed);
        var ns = new Nodeset("ns");
        for (uint i = 0; i < 300; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("field", "string");
        var cats = new Dictionary<uint, string>();
        string[] fields = ["a", "b", "c", "d", "e"];
        for (uint i = 0; i < 300; i++)
        {
            if (rng.Next(10) == 0) continue; // ~10% uncategorized
            // categories correlated with id blocks, so hyperedges drawn per block are assortative
            string f = rng.Next(4) == 0 ? fields[rng.Next(5)] : fields[i / 60];
            cats[i] = f;
            Assert.True(ns.SetNodeAttribute(i, "field", f).Success);
        }
        var net = new Network("net", ns);
        net.AddLayerTwoMode("h");
        for (int h = 0; h < 120; h++)
        {
            int size = rng.Next(10) == 0 ? 20 + rng.Next(40) : 1 + rng.Next(6);
            int block = rng.Next(5) * 60;
            var members = Enumerable.Range(0, size)
                .Select(_ => (uint)(rng.Next(3) == 0 ? rng.Next(300) : block + rng.Next(60)))
                .Distinct().ToArray();
            net.AddHyperedge("h", $"h{h}", members);
        }
        if (packed) Assert.True(net.Pack("h").Success);
        return (net, cats);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Categorical_Count_MatchesPairEnumeration(int seed, bool packed)
    {
        var (net, cats) = Make(seed, packed);
        var r = Analyses.Assortativity(net, "h", "field", ProjectionMethod.Count);
        Assert.True(r.Success, r.Message);
        Assert.Equal(Reference(net, "h", cats, binary: false), r.Value, 12);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void Categorical_Binary_MatchesPairEnumeration(int seed, bool packed)
    {
        var (net, cats) = Make(seed, packed);
        var r = Analyses.Assortativity(net, "h", "field", ProjectionMethod.Binary);
        Assert.True(r.Success, r.Message);
        Assert.Equal(Reference(net, "h", cats, binary: true), r.Value, 12);
    }

    [Fact]
    public void Categorical_Count_DiffersFromBinary_WhenPairsShareSeveralHyperedges()
    {
        // 0-1 (same category) share three hyperedges; 1-2 and 2-3 (different categories) one each
        var ns = new Nodeset("ns");
        for (uint i = 0; i < 4; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("field", "string");
        string[] f = ["x", "x", "y", "x"];
        for (uint i = 0; i < 4; i++) ns.SetNodeAttribute(i, "field", f[i]);
        var net = new Network("net", ns);
        net.AddLayerTwoMode("h");
        net.AddHyperedge("h", "a", [0, 1]);
        net.AddHyperedge("h", "b", [0, 1]);
        net.AddHyperedge("h", "c", [0, 1]);
        net.AddHyperedge("h", "d", [1, 2]);
        net.AddHyperedge("h", "e", [2, 3]);
        var cats = Enumerable.Range(0, 4).ToDictionary(i => (uint)i, i => f[i]);
        double count = Analyses.Assortativity(net, "h", "field", ProjectionMethod.Count).Value;
        double binary = Analyses.Assortativity(net, "h", "field", ProjectionMethod.Binary).Value;
        Assert.Equal(Reference(net, "h", cats, false), count, 12);
        Assert.Equal(Reference(net, "h", cats, true), binary, 12);
        Assert.True(count > binary);
    }
}