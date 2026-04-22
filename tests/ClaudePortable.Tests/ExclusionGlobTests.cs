using ClaudePortable.Core.Archive;

namespace ClaudePortable.Tests;

public class ExclusionGlobTests
{
    private static readonly ExclusionGlob Defaults = new(DefaultExclusions.Globs);

    [Theory]
    [InlineData("claude-desktop/appdata/Cache/000003.ldb", true)]
    [InlineData("claude-desktop/appdata/GPUCache/index", true)]
    [InlineData("claude-desktop/appdata/Code Cache/js/v8", true)]
    [InlineData("claude-desktop/appdata/Crashpad/reports/foo.dmp", true)]
    [InlineData("claude-desktop/appdata/Network/Cookies-journal", true)]
    [InlineData("claude-desktop/appdata/tokens.dat", true)]
    [InlineData("claude-desktop/appdata/Login Data", true)]
    [InlineData("claude-desktop/appdata/Login Data-journal", true)]
    [InlineData("claude-code/dotclaude/.remote-plugins/foo/bin.dll", true)]
    [InlineData("claude-code/dotclaude/plugins/my-plugin/skill.md", false)]
    [InlineData("claude-code/dotclaude/settings.json", false)]
    [InlineData("claude-code/dotclaude/CLAUDE.md", false)]
    [InlineData("claude-desktop/appdata/IndexedDB/data.leveldb/MANIFEST-000001", false)]
    [InlineData("claude-desktop/appdata/Preferences", false)]
    public void IsExcluded_MatchesExpectedPolicy(string path, bool expected)
    {
        Assert.Equal(expected, Defaults.IsExcluded(path));
    }

    [Fact]
    public void IsExcluded_IsCaseInsensitive()
    {
        Assert.True(Defaults.IsExcluded("claude-desktop/appdata/CACHE/foo"));
        Assert.True(Defaults.IsExcluded("claude-desktop/appdata/cache/foo"));
    }

    [Fact]
    public void IsExcluded_HandlesBackslashSeparators()
    {
        Assert.True(Defaults.IsExcluded(@"claude-desktop\appdata\Cache\foo"));
    }
}
