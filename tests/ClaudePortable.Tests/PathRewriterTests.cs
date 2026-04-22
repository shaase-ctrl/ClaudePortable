using ClaudePortable.Core.Restore;

namespace ClaudePortable.Tests;

public class PathRewriterTests
{
    [Fact]
    public void ReplaceIn_EscapedBackslashes_ReplacesOldUser()
    {
        var input = @"{""path"":""C:\\Users\\OldUser\\.claude\\settings.json""}";
        var (count, result) = PathRewriter.ReplaceIn(input, "OldUser", "NewUser");

        Assert.Equal(1, count);
        Assert.Equal(@"{""path"":""C:\\Users\\NewUser\\.claude\\settings.json""}", result);
    }

    [Fact]
    public void ReplaceIn_SingleBackslashes_ReplacesOldUser()
    {
        var input = @"settings = ""C:\Users\OldUser\AppData\Roaming\Claude\mcp.json""";
        var (count, result) = PathRewriter.ReplaceIn(input, "OldUser", "NewUser");

        Assert.Equal(1, count);
        Assert.Contains(@"C:\Users\NewUser\AppData\Roaming\Claude", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceIn_MultipleOccurrences_AllReplaced()
    {
        var input = @"[""C:\\Users\\Old\\.claude"", ""C:\\Users\\Old\\.cowork""]";
        var (count, result) = PathRewriter.ReplaceIn(input, "Old", "NewGuy");

        Assert.Equal(2, count);
        Assert.Contains(@"C:\\Users\\NewGuy\\.claude", result, StringComparison.Ordinal);
        Assert.Contains(@"C:\\Users\\NewGuy\\.cowork", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceIn_NoMatchingPath_ReturnsZero()
    {
        var input = @"{""unrelated"":""C:\\ProgramData\\foo""}";
        var (count, result) = PathRewriter.ReplaceIn(input, "OldUser", "NewUser");

        Assert.Equal(0, count);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ReplaceIn_SameOldAndNewUser_ReturnsZero()
    {
        var input = @"{""path"":""C:\\Users\\Sam\\.claude""}";
        var (count, result) = PathRewriter.ReplaceIn(input, "Sam", "Sam");

        Assert.Equal(0, count);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ReplaceIn_DifferentUserButSamePathTail_NotReplaced()
    {
        var input = @"{""path"":""C:\\Users\\Alice\\.claude""}";
        var (count, _) = PathRewriter.ReplaceIn(input, "Bob", "Charlie");

        Assert.Equal(0, count);
    }

    [Fact]
    public void Rewrite_ProcessesOnlyJsonFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cp-rewriter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var jsonPath = Path.Combine(root, "config.json");
            var mdPath = Path.Combine(root, "notes.md");
            File.WriteAllText(jsonPath, @"{""p"":""C:\\Users\\Alice\\.claude""}");
            File.WriteAllText(mdPath, @"See C:\Users\Alice\.claude for settings.");

            var rewriter = new PathRewriter();
            var result = rewriter.Rewrite(root, @"C:\Users\Alice", @"C:\Users\Bob");

            Assert.Equal(1, result.FilesScanned);
            Assert.Equal(1, result.FilesChanged);
            Assert.True(result.ReplacementsMade >= 1);

            var rewritten = File.ReadAllText(jsonPath);
            Assert.Contains("Bob", rewritten, StringComparison.Ordinal);
            var unchanged = File.ReadAllText(mdPath);
            Assert.Contains("Alice", unchanged, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
