using System.IO.Compression;
using System.Text;
using ClaudePortable.Core.Manifest;
using ClaudePortable.Scheduler.Retention;
using ClaudePortable.Targets;

namespace ClaudePortable.Tests;

public class RetentionManagerTests : IDisposable
{
    private readonly string _folder;

    public RetentionManagerTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"cp-retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Rotate_PromotesSundayDailyToWeekly()
    {
        var sunday = new DateTimeOffset(2026, 4, 19, 23, 0, 0, TimeSpan.Zero);
        WriteBackup(sunday, RetentionTier.Daily);

        var manager = new RetentionManager(
            RetentionPolicy.Default,
            new FakeClock(sunday.AddDays(1)));
        var target = new FolderTarget(_folder);

        var report = manager.Rotate(target);

        Assert.Single(report.Promoted);
        Assert.Equal(0, report.DailyAfter);
        Assert.Equal(1, report.WeeklyAfter);
        Assert.Contains(target.ListBackups(), b => b.Manifest!.RetentionTier == RetentionTier.Weekly);
    }

    [Fact]
    public void Rotate_DoesNotPromoteNonSundayDaily()
    {
        var tuesday = new DateTimeOffset(2026, 4, 21, 23, 0, 0, TimeSpan.Zero);
        WriteBackup(tuesday, RetentionTier.Daily);

        var manager = new RetentionManager(RetentionPolicy.Default, new FakeClock(tuesday.AddDays(1)));
        var report = manager.Rotate(new FolderTarget(_folder));

        Assert.Empty(report.Promoted);
        Assert.Equal(1, report.DailyAfter);
        Assert.Equal(0, report.WeeklyAfter);
    }

    [Fact]
    public void Rotate_PromotesLastMonthsWeeklyToMonthly()
    {
        var marchSunday = new DateTimeOffset(2026, 3, 29, 23, 0, 0, TimeSpan.Zero);
        WriteBackup(marchSunday, RetentionTier.Weekly);
        var now = new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero);

        var manager = new RetentionManager(RetentionPolicy.Default, new FakeClock(now));
        var report = manager.Rotate(new FolderTarget(_folder));

        Assert.Single(report.Promoted);
        Assert.Equal(0, report.WeeklyAfter);
        Assert.Equal(1, report.MonthlyAfter);
    }

    [Fact]
    public void Rotate_PrunesExcessDailiesOldestFirst()
    {
        var baseDate = new DateTimeOffset(2026, 4, 10, 23, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 10; i++)
        {
            WriteBackup(baseDate.AddDays(i), RetentionTier.Daily);
        }

        var policy = RetentionPolicy.Create(7, 0, 0, DayOfWeek.Sunday);
        var manager = new RetentionManager(policy, new FakeClock(baseDate.AddDays(11)));
        var report = manager.Rotate(new FolderTarget(_folder));

        Assert.Empty(report.Promoted);
        Assert.Equal(7, report.DailyAfter);
        Assert.Equal(3, report.Pruned.Count);
    }

    [Fact]
    public void Rotate_OverTenWeeks_ProducesExpectedFinalShape()
    {
        var start = new DateTimeOffset(2026, 1, 1, 23, 0, 0, TimeSpan.Zero);
        var totalDays = 70;
        var manager = new RetentionManager(RetentionPolicy.Default, new FakeClock(start));
        var target = new FolderTarget(_folder);

        for (var day = 0; day < totalDays; day++)
        {
            var when = start.AddDays(day);
            WriteBackup(when, RetentionTier.Daily);
            manager = new RetentionManager(RetentionPolicy.Default, new FakeClock(when.AddMinutes(1)));
            manager.Rotate(target);
        }

        var final = target.ListBackups()
            .Where(b => b.Manifest is not null)
            .GroupBy(b => b.Manifest!.RetentionTier)
            .ToDictionary(g => g.Key, g => g.Count());

        final.TryGetValue(RetentionTier.Daily, out var daily);
        final.TryGetValue(RetentionTier.Weekly, out var weekly);
        final.TryGetValue(RetentionTier.Monthly, out var monthly);

        Assert.True(daily <= 7);
        Assert.True(weekly <= 3);
        Assert.True(monthly <= 2);
        Assert.True(daily + weekly + monthly <= 12);
        Assert.True(monthly >= 1);
    }

    private void WriteBackup(DateTimeOffset createdAt, RetentionTier tier)
    {
        var iso = createdAt.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"claude-backup_{iso}_HOST_{tier.ToString().ToLowerInvariant()}.zip";
        var fullPath = Path.Combine(_folder, fileName);

        using var fs = File.Create(fullPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("manifest.json");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        var manifest = new BackupManifest
        {
            CreatedAt = createdAt,
            Hostname = "HOST",
            WindowsUser = "tester",
            RetentionTier = tier,
            SourcePaths = new Dictionary<string, string>(),
            SizeBytes = 0,
            FileCount = 0,
            Sha256 = "",
            ExcludedPaths = Array.Empty<string>(),
            ToolVersion = "test",
        };
        writer.Write(ManifestBuilder.Serialize(manifest));
    }

    private sealed class FakeClock : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
