namespace ClaudePortable.Scheduler.Retention;

public sealed record RetentionPolicy(int DailyKeep, int WeeklyKeep, int MonthlyKeep, DayOfWeek WeeklyAnchor)
{
    public static RetentionPolicy Default { get; } = new(7, 3, 2, DayOfWeek.Sunday);

    public static RetentionPolicy Create(int dailyKeep, int weeklyKeep, int monthlyKeep, DayOfWeek weeklyAnchor)
    {
        if (dailyKeep < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyKeep), "Must be at least 1.");
        }
        if (weeklyKeep < 0 || monthlyKeep < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weeklyKeep), "Retention counts must be non-negative.");
        }
        return new RetentionPolicy(dailyKeep, weeklyKeep, monthlyKeep, weeklyAnchor);
    }
}
