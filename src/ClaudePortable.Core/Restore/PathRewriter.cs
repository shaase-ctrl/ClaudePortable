using System.Text.RegularExpressions;
using ClaudePortable.Core.Abstractions;

namespace ClaudePortable.Core.Restore;

public sealed class PathRewriter : IPathRewriter
{
    private static readonly Regex JsonUserPathPattern = new(
        @"(?<drive>[A-Z]):\\\\Users\\\\(?<user>[^\\\\""]+)\\\\(?<tail>\.claude|AppData\\\\Roaming\\\\Claude|AppData\\\\Local\\\\Claude|\.cowork)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JsonUserPathPatternSingleBackslash = new(
        @"(?<drive>[A-Z]):\\Users\\(?<user>[^\\""]+)\\(?<tail>\.claude|AppData\\Roaming\\Claude|AppData\\Local\\Claude|\.cowork)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PathRewriteResult Rewrite(string rootFolder, string oldUserProfile, string newUserProfile)
    {
        if (!Directory.Exists(rootFolder))
        {
            return new PathRewriteResult(0, 0, 0);
        }

        var filesScanned = 0;
        var filesChanged = 0;
        var replacementsMade = 0;

        var oldUserName = Path.GetFileName(oldUserProfile.TrimEnd('\\', '/'));
        var newUserName = Path.GetFileName(newUserProfile.TrimEnd('\\', '/'));

        foreach (var file in Directory.EnumerateFiles(rootFolder, "*.json", SearchOption.AllDirectories))
        {
            filesScanned++;
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }

            var (replaced, newContent) = ReplaceIn(content, oldUserName, newUserName);
            if (replaced > 0)
            {
                File.WriteAllText(file, newContent);
                filesChanged++;
                replacementsMade += replaced;
            }
        }

        return new PathRewriteResult(filesScanned, filesChanged, replacementsMade);
    }

    internal static (int Replacements, string NewContent) ReplaceIn(string content, string oldUserName, string newUserName)
    {
        if (string.IsNullOrEmpty(oldUserName) || string.Equals(oldUserName, newUserName, StringComparison.OrdinalIgnoreCase))
        {
            return (0, content);
        }

        var count = 0;
        var stage1 = JsonUserPathPattern.Replace(content, m =>
        {
            var user = m.Groups["user"].Value;
            if (!string.Equals(user, oldUserName, StringComparison.OrdinalIgnoreCase))
            {
                return m.Value;
            }
            count++;
            var drive = m.Groups["drive"].Value;
            var tail = m.Groups["tail"].Value;
            return $@"{drive}:\\Users\\{newUserName}\\{tail}";
        });

        var stage2 = JsonUserPathPatternSingleBackslash.Replace(stage1, m =>
        {
            var user = m.Groups["user"].Value;
            if (!string.Equals(user, oldUserName, StringComparison.OrdinalIgnoreCase))
            {
                return m.Value;
            }
            count++;
            var drive = m.Groups["drive"].Value;
            var tail = m.Groups["tail"].Value;
            return $@"{drive}:\Users\{newUserName}\{tail}";
        });

        return (count, stage2);
    }
}
