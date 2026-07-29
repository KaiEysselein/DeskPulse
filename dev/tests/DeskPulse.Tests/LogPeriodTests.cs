using DeskPulse;
using Xunit;

namespace DeskPulse.Tests;

public sealed class LogPeriodTests
{
    private static readonly DateTime Now = new(2026, 7, 28, 15, 42, 30);

    [Theory]
    [InlineData("Last 24 hours", -24)]
    [InlineData("Last 7 days", -168)]
    [InlineData("Last 30 days", -720)]
    [InlineData("Last 180 days", -4320)]
    public void RollingPeriodsPreserveExactTime(string period, int expectedHours)
    {
        var range = ViewLogForm.CalculatePeriodRange(period, Now);

        Assert.Equal(Now.AddHours(expectedHours), range.Start);
        Assert.Equal(Now, range.End);
    }

    [Fact]
    public void TodayStartsAtMidnight()
    {
        var range = ViewLogForm.CalculatePeriodRange("Today (since midnight)", Now);

        Assert.Equal(Now.Date, range.Start);
        Assert.Equal(Now, range.End);
    }

    [Fact]
    public void ThisMonthStartsAtMidnightOnFirstDay()
    {
        var range = ViewLogForm.CalculatePeriodRange("This month", Now);

        Assert.Equal(new DateTime(2026, 7, 1), range.Start);
        Assert.Equal(Now, range.End);
    }

    [Fact]
    public void YearPeriodsPreserveDayAndTime()
    {
        Assert.Equal(Now.AddYears(-1), ViewLogForm.CalculatePeriodRange("Last 1 year", Now).Start);
        Assert.Equal(Now.AddYears(-10), ViewLogForm.CalculatePeriodRange("Last 10 years", Now).Start);
    }

    [Fact]
    public void AllTimeStartsAtFirstRecordedDate()
    {
        var firstRecorded = new DateTime(2021, 3, 14, 9, 26, 53);

        var range = ViewLogForm.CalculatePeriodRange("All time", Now, firstRecorded);

        Assert.Equal(firstRecorded, range.Start);
        Assert.Equal(Now, range.End);
    }
}

public sealed class DeletionExclusionRuleTests
{
    private static readonly LogViewEntry Entry = new(
        "42",
        "2026-07-28T15:42:30",
        "28/07/2026",
        "15:42:30",
        "preview.png",
        @"C:\Images",
        "paint",
        "123",
        @"C:\Images\preview.png",
        new Dictionary<string, string> { ["Extension"] = ".png" });

    [Theory]
    [InlineData(DeleteLogRecordsForm.ExactFilePath, "file", @"C:\Images\preview.png", false)]
    [InlineData(DeleteLogRecordsForm.FileNameAnywhere, "file", "preview.png", false)]
    [InlineData(DeleteLogRecordsForm.FileExtension, "file", "*.png", false)]
    [InlineData(DeleteLogRecordsForm.Folder, "folder", @"C:\Images", true)]
    [InlineData(DeleteLogRecordsForm.Application, "process", "paint", false)]
    public void FileDeletionRuleUsesSelectedMatchType(
        string matchType,
        string expectedRuleType,
        string expectedValue,
        bool expectedIncludeSubfolders)
    {
        var rule = ViewLogForm.BuildDeletionExclusionRule(Entry, LogRuleCategory.File, matchType);

        Assert.NotNull(rule);
        Assert.Equal(expectedRuleType, rule.RuleType);
        Assert.Equal(expectedValue, rule.Value);
        Assert.Equal(expectedIncludeSubfolders, rule.IncludeSubfolders);
        Assert.Equal("Exclude", rule.Action);
    }
}
