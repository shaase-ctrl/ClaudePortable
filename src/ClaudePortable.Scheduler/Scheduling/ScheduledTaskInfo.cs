namespace ClaudePortable.Scheduler.Scheduling;

public enum ManagedBy
{
    ClaudePortable,
    ForeignRelevant,
    ForeignOther,
}

public sealed record ScheduledTaskAction(
    string Executable,
    string Arguments,
    string WorkingDirectory);

public sealed record ScheduledTaskInfo(
    string Name,
    string FolderPath,
    string State,
    DateTimeOffset? NextRunTime,
    DateTimeOffset? LastRunTime,
    int? LastResult,
    string? Author,
    ScheduledTaskAction Action,
    string TriggerSummary,
    ManagedBy ManagedBy,
    string? RawXml = null)
{
    public string FullName => string.IsNullOrEmpty(FolderPath) || FolderPath == "\\"
        ? "\\" + Name
        : FolderPath + "\\" + Name;
}
