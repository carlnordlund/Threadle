using Threadle.Core.Utilities;

namespace Threadle.Tests;

[Collection("MaxThreadsLimit")] // asserts the global 'maxthreads' value, which other test classes change while running
public class UserSettingsTests
{
    // ── Removed settings ────────────────────────────────────────────────────

    [Theory]
    [InlineData("nodecache")]
    [InlineData("blockmultiedges")]
    public void Set_RemovedSetting_IsUnknown(string name)
    {
        var result = UserSettings.Set(name, "false");
        Assert.False(result.Success);
        Assert.Equal("SettingNotFound", result.Code);
    }

    [Fact]
    public void Set_MaxThreads_InvalidStringValue_Fails()
    {
        var result = UserSettings.Set("maxthreads", "notanint");
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