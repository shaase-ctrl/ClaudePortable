namespace ClaudePortable.Scheduler.Scheduling;

public static class ScheduledTaskClassifier
{
    public static readonly IReadOnlyList<string> RelevanceMarkers = new[]
    {
        "ClaudePortable",
        ".claude",
        @"\Claude\",
        "Claude_pzs8sxrjxfjjc",
        "Cowork",
        @"CoWork\Backup",
        "local-agent-mode-sessions",
        "claude-desktop",
        "anthropic",
    };

    public static ManagedBy Classify(ScheduledTaskInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (IsClaudePortableManaged(info))
        {
            return ManagedBy.ClaudePortable;
        }

        if (HasRelevanceMarker(info))
        {
            return ManagedBy.ForeignRelevant;
        }

        return ManagedBy.ForeignOther;
    }

    private static bool IsClaudePortableManaged(ScheduledTaskInfo info)
    {
        var bareName = StripFolder(info.Name);
        if (bareName.StartsWith("ClaudePortable-", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("ClaudePortable", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(info.Author)
            && info.Author.Contains("ClaudePortable", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool HasRelevanceMarker(ScheduledTaskInfo info)
    {
        var haystacks = new[]
        {
            info.Name,
            info.FolderPath,
            info.Author ?? string.Empty,
            info.Action.Executable,
            info.Action.Arguments,
            info.Action.WorkingDirectory,
        };

        foreach (var marker in RelevanceMarkers)
        {
            foreach (var h in haystacks)
            {
                if (!string.IsNullOrEmpty(h) && h.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string StripFolder(string name)
    {
        var i = name.LastIndexOf('\\');
        return i < 0 ? name : name[(i + 1)..];
    }
}
