namespace ClaudePortable.Scheduler.Retention;

public sealed record RotationReport(
    IReadOnlyList<string> Promoted,
    IReadOnlyList<string> Pruned,
    int DailyAfter,
    int WeeklyAfter,
    int MonthlyAfter)
{
    public static RotationReport Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        DailyAfter: 0,
        WeeklyAfter: 0,
        MonthlyAfter: 0);
}
