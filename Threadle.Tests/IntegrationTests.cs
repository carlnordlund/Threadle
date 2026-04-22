using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Processing.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Integration tests that chain multiple operations together using real data files
/// (the Lazega lawyers dataset bundled with the CLI examples). Each test simulates
/// a realistic analysis workflow: load → transform → analyse → optionally save and reload.
/// </summary>
public class IntegrationTests : IDisposable
{
    // ── Paths ──────────────────────────────────────────────────────────────────

    // Resolve path to the bundled example files regardless of where the test runner
    // places its output directory (typically bin/Debug/net8.0/).
    private static readonly string ExamplesDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Threadle.CLIconsole", "Examples"));

    private static string LazegaNetPath => Path.Combine(ExamplesDir, "lazega.tsv");

    // ── Temp-file cleanup ──────────────────────────────────────────────────────

    private readonly List<string> _tempFiles = [];

    private string TempFile(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), $"threadle_integration_{Guid.NewGuid()}{extension}");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f))
                File.Delete(f);
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static Network LoadLazega()
    {
        var result = FileManager.Load(LazegaNetPath, "network");
        Assert.True(result.Success, $"Loading Lazega failed: {result.Message}");
        return (Network)result.Value!.MainStructure;
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loading the Lazega file should yield a network with 71 lawyers across three
    /// named layers with the expected directionalities.
    /// </summary>
    [Fact]
    public void Lazega_Load_ReturnsCorrectStructure()
    {
        var net = LoadLazega();

        Assert.Equal(71, net.Nodeset.Count);
        Assert.Equal(3, net.Layers.Count);
        Assert.True(net.Layers.ContainsKey("friends"));
        Assert.True(net.Layers.ContainsKey("advice"));
        Assert.True(net.Layers.ContainsKey("collaboration"));

        var friends = (ILayerOneMode)net.Layers["friends"];
        var collab  = (ILayerOneMode)net.Layers["collaboration"];

        Assert.Equal(EdgeDirectionality.Directed,   friends.Directionality);
        Assert.Equal(EdgeDirectionality.Undirected, collab.Directionality);
    }

    /// <summary>
    /// Every node in the Lazega nodeset should carry all seven attributes defined
    /// in the file header (Status, Gender, Office, YearsWithFirm, Age, Practice, LawSchool).
    /// </summary>
    [Fact]
    public void Lazega_NodeAttributes_AllNodesHaveAllSevenAttributes()
    {
        var net = LoadLazega();
        string[] attrs = ["Status", "Gender", "Office", "YearsWithFirm", "Age", "Practice", "LawSchool"];

        foreach (uint nodeId in net.Nodeset.NodeIdArray)
        {
            foreach (string attr in attrs)
            {
                var r = net.Nodeset.GetNodeAttribute(nodeId, attr);
                Assert.True(r.Success, $"Node {nodeId} missing attribute '{attr}'");
            }
        }
    }

    /// <summary>
    /// Computing out-degree centrality on the directed 'friends' layer should store
    /// an attribute on every node. Node 17 — a well-known hub in this dataset — must
    /// have strictly higher out-degree than node 71, which sends no friendship nominations.
    /// </summary>
    [Fact]
    public void Lazega_OutDegreeCentrality_Friends_StoredForAllNodes_And_HubOutranksSink()
    {
        var net = LoadLazega();

        var result = Analyses.DegreeCentralities(net, "friends", "out_deg", EdgeTraversal.Out);
        Assert.True(result.Success, result.Message);

        foreach (uint nodeId in net.Nodeset.NodeIdArray)
        {
            var r = net.Nodeset.GetNodeAttribute(nodeId, "out_deg");
            Assert.True(r.Success, $"Node {nodeId} missing 'out_deg' after DegreeCentralities");
        }

        // Node 17 nominates 23 friends; node 71 nominates none.
        int deg17 = GetIntAttr(net.Nodeset, 17, "out_deg");
        int deg71 = GetIntAttr(net.Nodeset, 71, "out_deg");
        Assert.True(deg17 > deg71, $"Expected deg17 ({deg17}) > deg71 ({deg71})");
    }

    /// <summary>
    /// Filtering the Lazega nodeset to partners only (Status == "P") should yield
    /// exactly 36 nodes, and creating a subnet should preserve all three layers while
    /// containing only partner nodes.
    /// </summary>
    [Fact]
    public void Lazega_PartnerFilter_SubnetHas36NodesAndThreeLayers()
    {
        var net = LoadLazega();

        var filterResult = NodesetProcessor.Filter(net.Nodeset, "Status", ConditionType.eq, "P");
        Assert.True(filterResult.Success, filterResult.Message);
        Nodeset partnerNs = filterResult.Value!;

        Assert.Equal(36, partnerNs.Count);

        var subnetResult = NetworkProcessor.Subnet(net, partnerNs);
        Assert.True(subnetResult.Success, subnetResult.Message);
        Network subnet = subnetResult.Value!;

        Assert.Equal(36, subnet.Nodeset.Count);
        Assert.Equal(3,  subnet.Layers.Count);

        // Every node in the subnet should still carry a "P" status.
        // Use GetNodeAttribute because "Status" is a Char type, not a String type.
        foreach (uint nodeId in subnet.Nodeset.NodeIdArray)
        {
            var r = subnet.Nodeset.GetNodeAttribute(nodeId, "Status");
            Assert.True(r.Success, $"Node {nodeId}: missing Status attribute");
            Assert.Equal("P", r.Value.Value.GetValue(r.Value.Type)?.ToString());
        }
    }

    /// <summary>
    /// Computes density on all three Lazega layers. Each density must lie strictly
    /// between 0 and 1. Additionally, the collaboration layer (undirected) should be
    /// less dense than the advice layer (directed) — a well-known property of this dataset.
    /// </summary>
    [Fact]
    public void Lazega_Density_AllThreeLayers_InValidRange()
    {
        var net = LoadLazega();

        var rFriends = Analyses.Density(net, "friends");
        var rAdvice  = Analyses.Density(net, "advice");
        var rCollab  = Analyses.Density(net, "collaboration");

        Assert.True(rFriends.Success, rFriends.Message);
        Assert.True(rAdvice.Success,  rAdvice.Message);
        Assert.True(rCollab.Success,  rCollab.Message);

        Assert.True(rFriends.Value > 0.0 && rFriends.Value < 1.0, $"Friends density out of range: {rFriends.Value}");
        Assert.True(rAdvice.Value  > 0.0 && rAdvice.Value  < 1.0, $"Advice density out of range: {rAdvice.Value}");
        Assert.True(rCollab.Value  > 0.0 && rCollab.Value  < 1.0, $"Collab density out of range: {rCollab.Value}");

        // Collaboration networks are typically sparser than advice networks.
        Assert.True(rCollab.Value < rAdvice.Value,
            $"Expected collab density ({rCollab.Value:F4}) < advice density ({rAdvice.Value:F4})");
    }

    /// <summary>
    /// A full multi-step workflow: load the Lazega network, add a generated
    /// Erdős–Rényi layer, redirect the nodeset to a temp file so the original
    /// data is not overwritten, save the modified network, reload it, and verify
    /// that all four layers survive the round-trip.
    /// </summary>
    [Fact]
    public void Lazega_AddERLayer_SaveAndReload_FourLayersPreserved()
    {
        var net = LoadLazega();

        net.AddLayerOneMode("random", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var genResult = Generators.GenerateErdosRenyiLayer(net, "random", p: 0.05);
        Assert.True(genResult.Success, genResult.Message);

        // Save the nodeset to a temp file first so the original lazega_nodes.tsv is untouched.
        // Saving separately also guarantees the file physically exists on disk before
        // SaveNetwork writes the NodesetFile reference into the network file.
        string tempNsPath  = TempFile(".tsv");
        string tempNetPath = TempFile(".tsv");
        Assert.True(FileManager.Save(net.Nodeset, tempNsPath).Success, "Nodeset save failed");

        var saveResult = FileManager.Save(net, tempNetPath);
        Assert.True(saveResult.Success, saveResult.Message);

        var loadResult = FileManager.Load(tempNetPath, "network");
        Assert.True(loadResult.Success, loadResult.Message);
        var reloaded = (Network)loadResult.Value!.MainStructure;

        Assert.Equal(4, reloaded.Layers.Count);
        Assert.True(reloaded.Layers.ContainsKey("friends"));
        Assert.True(reloaded.Layers.ContainsKey("advice"));
        Assert.True(reloaded.Layers.ContainsKey("collaboration"));
        Assert.True(reloaded.Layers.ContainsKey("random"));
    }

    /// <summary>
    /// End-to-end pipeline: load Lazega → compute out-degree on all three layers
    /// → filter nodeset to Boston-office lawyers → extract their subnet
    /// → verify that the degree attribute round-trips through a save/reload cycle.
    /// </summary>
    [Fact]
    public void Lazega_FullPipeline_Analyze_Filter_SaveSubnet_ReloadVerifyAttributes()
    {
        var net = LoadLazega();

        // Step 1: compute out-degree centrality on all three layers.
        Assert.True(Analyses.DegreeCentralities(net, "friends",       "deg_friends", EdgeTraversal.Out).Success);
        Assert.True(Analyses.DegreeCentralities(net, "advice",        "deg_advice",  EdgeTraversal.Out).Success);
        Assert.True(Analyses.DegreeCentralities(net, "collaboration", "deg_collab"                    ).Success);

        // Step 2: filter to Boston-office lawyers (Office == "B").
        var filterResult = NodesetProcessor.Filter(net.Nodeset, "Office", ConditionType.eq, "B");
        Assert.True(filterResult.Success, filterResult.Message);
        Nodeset bostonNs = filterResult.Value!;
        Assert.True(bostonNs.Count > 0, "Expected at least one Boston-office lawyer");

        // Step 3: create the Boston subnet.
        var subnetResult = NetworkProcessor.Subnet(net, bostonNs);
        Assert.True(subnetResult.Success, subnetResult.Message);
        Network bostonNet = subnetResult.Value!;

        // Step 4: save the subnet. Save the nodeset first so the original files are untouched
        // and the nodeset file physically exists before the network file references it.
        string tempNsPath  = TempFile(".tsv");
        string tempNetPath = TempFile(".tsv");
        Assert.True(FileManager.Save(bostonNet.Nodeset, tempNsPath).Success, "Nodeset save failed");
        Assert.True(FileManager.Save(bostonNet, tempNetPath).Success, "Network save failed");

        // Step 5: reload and verify degree attributes survived.
        var loadResult = FileManager.Load(tempNetPath, "network");
        Assert.True(loadResult.Success, loadResult.Message);
        var reloaded = (Network)loadResult.Value!.MainStructure;

        foreach (uint nodeId in reloaded.Nodeset.NodeIdArray)
        {
            Assert.True(reloaded.Nodeset.GetNodeAttribute(nodeId, "deg_friends").Success,
                $"Node {nodeId}: 'deg_friends' attribute missing after reload");
            Assert.True(reloaded.Nodeset.GetNodeAttribute(nodeId, "deg_advice").Success,
                $"Node {nodeId}: 'deg_advice' attribute missing after reload");
            Assert.True(reloaded.Nodeset.GetNodeAttribute(nodeId, "deg_collab").Success,
                $"Node {nodeId}: 'deg_collab' attribute missing after reload");
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static int GetIntAttr(Nodeset ns, uint nodeId, string attrName)
    {
        var r = ns.GetNodeAttribute(nodeId, attrName);
        Assert.True(r.Success);
        return (int)r.Value.Value.GetValue(r.Value.Type)!;
    }
}
