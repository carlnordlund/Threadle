using Threadle.Core.Utilities;

namespace Threadle.Tests;

public class UserSettingsTests
{
    // ── Set(string, object) — bool settings, typed value ────────────────────

    [Fact]
    public void Set_NodeCacheBoolValue_Succeeds()
    {
        bool original = UserSettings.NodeCache;
        try
        {
            var result = UserSettings.Set("nodecache", false);
            Assert.True(result.Success);
            Assert.False(UserSettings.NodeCache);
        }
        finally
        {
            UserSettings.NodeCache = original;
        }
    }

    // ── Set(string, object) — bool settings, string value (as from the CLI) ─

    [Fact]
    public void Set_NodeCacheStringValue_Succeeds()
    {
        bool original = UserSettings.NodeCache;
        try
        {
            var result = UserSettings.Set("nodecache", "false");
            Assert.True(result.Success);
            Assert.False(UserSettings.NodeCache);
        }
        finally
        {
            UserSettings.NodeCache = original;
        }
    }

    [Fact]
    public void Set_BoolSetting_InvalidStringValue_Fails()
    {
        var result = UserSettings.Set("nodecache", "notabool");
        Assert.False(result.Success);
        Assert.Equal("InvalidValue", result.Code);
    }

    // ── Set(string, object) — maxthreads (int setting) ──────────────────────

    [Fact]
    public void Set_MaxThreadsUnconstrained_Succeeds()
    {
        try
        {
            var result = UserSettings.Set("maxthreads", -1);
            Assert.True(result.Success);
            Assert.Equal(-1, UserSettings.MaxDegreeOfParallelism);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void Set_MaxThreadsPositiveIntValue_Succeeds()
    {
        try
        {
            var result = UserSettings.Set("maxthreads", 4);
            Assert.True(result.Success);
            Assert.Equal(4, UserSettings.MaxDegreeOfParallelism);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void Set_MaxThreadsPositiveStringValue_Succeeds()
    {
        try
        {
            var result = UserSettings.Set("maxthreads", "4");
            Assert.True(result.Success);
            Assert.Equal(4, UserSettings.MaxDegreeOfParallelism);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void Set_MaxThreadsZero_Fails()
    {
        var result = UserSettings.Set("maxthreads", 0);
        Assert.False(result.Success);
        Assert.Equal(-1, UserSettings.MaxDegreeOfParallelism);
    }

    [Fact]
    public void Set_MaxThreadsNegativeOtherThanMinusOne_Fails()
    {
        var result = UserSettings.Set("maxthreads", -2);
        Assert.False(result.Success);
        Assert.Equal(-1, UserSettings.MaxDegreeOfParallelism);
    }

    [Fact]
    public void Set_MaxThreadsExceedingProcessorCount_Fails()
    {
        int tooMany = Environment.ProcessorCount + 1;
        var result = UserSettings.Set("maxthreads", tooMany);
        Assert.False(result.Success);
        Assert.Equal(-1, UserSettings.MaxDegreeOfParallelism);
    }

    [Fact]
    public void Set_MaxThreadsEqualToProcessorCount_Succeeds()
    {
        try
        {
            var result = UserSettings.Set("maxthreads", Environment.ProcessorCount);
            Assert.True(result.Success);
            Assert.Equal(Environment.ProcessorCount, UserSettings.MaxDegreeOfParallelism);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void Set_MaxThreadsNonIntegerStringValue_Fails()
    {
        var result = UserSettings.Set("maxthreads", "notanumber");
        Assert.False(result.Success);
        Assert.Equal("InvalidValue", result.Code);
    }

    // ── Set(string, object) — unknown setting ────────────────────────────────

    [Fact]
    public void Set_UnknownSetting_Fails()
    {
        var result = UserSettings.Set("notasetting", 4);
        Assert.False(result.Success);
        Assert.Equal("SettingNotFound", result.Code);
    }

    // ── GetParallelOptions ───────────────────────────────────────────────────

    [Fact]
    public void GetParallelOptions_ReflectsCurrentSetting()
    {
        try
        {
            UserSettings.Set("maxthreads", 3);
            var options = UserSettings.GetParallelOptions();
            Assert.Equal(3, options.MaxDegreeOfParallelism);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }
}