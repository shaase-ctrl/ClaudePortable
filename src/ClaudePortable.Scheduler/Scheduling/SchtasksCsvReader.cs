using System.Globalization;
using System.Text;

namespace ClaudePortable.Scheduler.Scheduling;

internal static class SchtasksCsvReader
{
    public static List<List<string>> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var hasContent = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else
            {
                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        hasContent = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        hasContent = true;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        if (hasContent)
                        {
                            row.Add(field.ToString());
                            rows.Add(row);
                            row = new List<string>();
                            field.Clear();
                            hasContent = false;
                        }
                        break;
                    default:
                        field.Append(c);
                        hasContent = true;
                        break;
                }
            }
        }

        if (hasContent)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}

internal static class ScheduledTaskCsvParser
{
    public static IReadOnlyList<ScheduledTaskInfo> Parse(string csv)
    {
        var rows = SchtasksCsvReader.Parse(csv);
        if (rows.Count <= 1)
        {
            return Array.Empty<ScheduledTaskInfo>();
        }

        var byName = new Dictionary<string, RawTaskAccumulator>(StringComparer.OrdinalIgnoreCase);
        var headerCount = rows[0].Count;

        for (var i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.Count < headerCount || r[0].Equals(rows[0][0], StringComparison.Ordinal))
            {
                continue;
            }

            var taskName = SafeGet(r, 1);
            if (string.IsNullOrWhiteSpace(taskName) || taskName.Equals("INFO:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!byName.TryGetValue(taskName, out var acc))
            {
                acc = new RawTaskAccumulator(taskName);
                byName[taskName] = acc;
            }
            acc.AddRow(r);
        }

        var result = new List<ScheduledTaskInfo>(byName.Count);
        foreach (var acc in byName.Values)
        {
            result.Add(acc.Build());
        }

        return result;
    }

    private static string SafeGet(List<string> row, int index)
        => index < row.Count ? row[index] : string.Empty;

    private sealed class RawTaskAccumulator
    {
        private readonly string _taskName;
        private List<string>? _primary;
        private readonly List<string> _schedules = new();

        public RawTaskAccumulator(string taskName)
        {
            _taskName = taskName;
        }

        public void AddRow(List<string> row)
        {
            _primary ??= row;
            var sched = SafeGet(row, 17);
            if (!string.IsNullOrWhiteSpace(sched) && !_schedules.Contains(sched, StringComparer.OrdinalIgnoreCase))
            {
                _schedules.Add(sched);
            }
        }

        public ScheduledTaskInfo Build()
        {
            var r = _primary ?? new List<string>();
            var (folder, name) = SplitFolder(_taskName);
            var nextRun = ParseDate(SafeGet(r, 2));
            var status = SafeGet(r, 3);
            var lastRun = ParseDate(SafeGet(r, 5));
            var lastResult = ParseInt(SafeGet(r, 6));
            var author = NormaliseEmpty(SafeGet(r, 7));
            var taskToRun = SafeGet(r, 8);
            var startIn = SafeGet(r, 9);
            var schedType = SafeGet(r, 18);
            var startTime = SafeGet(r, 19);

            var (exe, args) = SplitExecutableAndArgs(taskToRun);
            var action = new ScheduledTaskAction(exe, args, startIn);

            var triggerSummary = BuildTriggerSummary(schedType, startTime);

            var info = new ScheduledTaskInfo(
                Name: name,
                FolderPath: folder,
                State: status,
                NextRunTime: nextRun,
                LastRunTime: lastRun,
                LastResult: lastResult,
                Author: author,
                Action: action,
                TriggerSummary: triggerSummary,
                ManagedBy: ManagedBy.ForeignOther);

            return info with { ManagedBy = ScheduledTaskClassifier.Classify(info) };
        }

        private string BuildTriggerSummary(string scheduleType, string startTime)
        {
            if (_schedules.Count > 1)
            {
                return $"{_schedules.Count} triggers";
            }
            if (!string.IsNullOrWhiteSpace(scheduleType))
            {
                var st = scheduleType.Trim();
                return IsNotApplicable(startTime) ? st : $"{st} @ {startTime.Trim()}";
            }
            return _schedules.Count == 1 ? _schedules[0] : "-";
        }
    }

    private static (string Folder, string Name) SplitFolder(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            return (string.Empty, string.Empty);
        }
        var trimmed = fullName.TrimStart('\\');
        var i = trimmed.LastIndexOf('\\');
        if (i < 0)
        {
            return ("\\", trimmed);
        }
        return ("\\" + trimmed[..i], trimmed[(i + 1)..]);
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var v = value.Trim();
        if (v.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("Nicht ", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("Never", StringComparison.OrdinalIgnoreCase)
            || v.Equals("Nie", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cultures = new[]
        {
            CultureInfo.CurrentCulture,
            CultureInfo.GetCultureInfo("de-DE"),
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.InvariantCulture,
        };
        foreach (var c in cultures)
        {
            if (DateTimeOffset.TryParse(v, c, DateTimeStyles.AssumeLocal, out var dt))
            {
                return dt;
            }
        }
        return null;
    }

    private static int? ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var v = value.Trim();
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static string? NormaliseEmpty(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var v = value.Trim();
        if (v.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            || v.Equals("Nicht zutreffend", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return v;
    }

    private static bool IsNotApplicable(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        var v = value.Trim();
        return v.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            || v.Equals("Nicht zutreffend", StringComparison.OrdinalIgnoreCase);
    }

    internal static (string Executable, string Arguments) SplitExecutableAndArgs(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return (string.Empty, string.Empty);
        }
        var s = commandLine.Trim();
        if (s.StartsWith('"'))
        {
            var end = s.IndexOf('"', 1);
            if (end > 0)
            {
                var exe = s[1..end];
                var rest = end + 1 < s.Length ? s[(end + 1)..].TrimStart() : string.Empty;
                return (exe, rest);
            }
        }

        var spaceIdx = s.IndexOf(' ', StringComparison.Ordinal);
        if (spaceIdx < 0)
        {
            return (s, string.Empty);
        }
        return (s[..spaceIdx], s[(spaceIdx + 1)..].TrimStart());
    }
}
