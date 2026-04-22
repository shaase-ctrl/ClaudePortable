namespace ClaudePortable.Core.Abstractions;

public sealed record PathRewriteResult(int FilesScanned, int FilesChanged, int ReplacementsMade);

public interface IPathRewriter
{
    PathRewriteResult Rewrite(
        string rootFolder,
        string oldUserProfile,
        string newUserProfile);
}
