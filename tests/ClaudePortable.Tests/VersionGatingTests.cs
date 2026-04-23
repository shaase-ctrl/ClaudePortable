using ClaudePortable.Core.Restore;

namespace ClaudePortable.Tests;

public class VersionGatingTests
{
    [Fact]
    public void SameVersion_ReturnsOk()
    {
        var r = VersionGating.Evaluate("1.3883.0.0", "1.3883.0.0");
        Assert.Equal(VersionGateLevel.Ok, r.Level);
    }

    [Fact]
    public void MinorDiff_ReturnsWarn()
    {
        var r = VersionGating.Evaluate("1.3883.0.0", "1.3900.0.0");
        Assert.Equal(VersionGateLevel.Warn, r.Level);
        Assert.Contains("Minor-version mismatch", r.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MajorDiff_ReturnsBlock()
    {
        var r = VersionGating.Evaluate("1.3883.0.0", "2.0.0.0");
        Assert.Equal(VersionGateLevel.Block, r.Level);
        Assert.Contains("Major-version mismatch", r.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InstalledMissing_ReturnsWarn()
    {
        var r = VersionGating.Evaluate("1.3883.0.0", null);
        Assert.Equal(VersionGateLevel.Warn, r.Level);
        Assert.Contains("not installed", r.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BackupMissing_ReturnsInfo()
    {
        var r = VersionGating.Evaluate(null, "1.3883.0.0");
        Assert.Equal(VersionGateLevel.Info, r.Level);
    }

    [Fact]
    public void BothMissing_ReturnsInfo()
    {
        var r = VersionGating.Evaluate(null, null);
        Assert.Equal(VersionGateLevel.Info, r.Level);
    }

    [Fact]
    public void UnparseableVersion_ReturnsWarn()
    {
        var r = VersionGating.Evaluate("garbage", "also-garbage");
        Assert.Equal(VersionGateLevel.Warn, r.Level);
    }

    [Theory]
    [InlineData("1.3883", "1.3883")]
    [InlineData("1.3883.0", "1.3883.0")]
    public void ShortVersionStrings_MatchWhenEqual(string b, string i)
    {
        var r = VersionGating.Evaluate(b, i);
        Assert.Equal(VersionGateLevel.Ok, r.Level);
    }
}
