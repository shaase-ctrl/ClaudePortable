namespace ClaudePortable.Scheduler.Scheduling;

public sealed record ScheduleSpec(
    string TaskName,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    TimeOnly DailyStart,
    string? Description = null,
    bool WakeToRun = false);
