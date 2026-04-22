using System.Text.RegularExpressions;

namespace ClaudePortable.Core.Archive;

public sealed class ExclusionGlob
{
    private readonly List<Regex> _compiled;

    public ExclusionGlob(IEnumerable<string> globs)
    {
        _compiled = globs.Select(Compile).ToList();
    }

    public bool IsExcluded(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return _compiled.Any(rx => rx.IsMatch(normalized));
    }

    private static Regex Compile(string glob)
    {
        var pattern = new System.Text.StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                pattern.Append(".*");
                i++;
                if (i + 1 < glob.Length && glob[i + 1] == '/')
                {
                    i++;
                }
            }
            else if (c == '*')
            {
                pattern.Append("[^/]*");
            }
            else if (c == '?')
            {
                pattern.Append("[^/]");
            }
            else if ("+()^$.{}[]|\\".Contains(c))
            {
                pattern.Append('\\').Append(c);
            }
            else
            {
                pattern.Append(c);
            }
        }
        pattern.Append('$');
        return new Regex(pattern.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
