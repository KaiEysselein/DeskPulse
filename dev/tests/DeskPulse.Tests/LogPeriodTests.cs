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
        false,
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

public sealed class CellRuleSuggestionTests
{
    private static readonly LogViewEntry Entry = new(
        "42",
        "2026-07-28T15:42:30",
        "28/07/2026",
        "15:42:30",
        "preview.png",
        @"C:\Images",
        "paint.exe",
        "123",
        @"C:\Program Files\Paint\paint.exe",
        false,
        new Dictionary<string, string> { ["Extension"] = ".png" });

    [Theory]
    [InlineData("File", LogRuleCategory.File, "file", "preview.png", false)]
    [InlineData("Extension", LogRuleCategory.File, "file", "*.png", false)]
    [InlineData("Folder", LogRuleCategory.File, "folder", @"C:\Images", true)]
    [InlineData("App", LogRuleCategory.App, "process", "paint.exe", false)]
    public void FileActivityCellDeterminesRule(
        string column,
        LogRuleCategory expectedFormCategory,
        string expectedRuleType,
        string expectedValue,
        bool expectedIncludeSubfolders)
    {
        var result = ViewLogForm.TryBuildCellRuleSuggestion(
            LogRuleCategory.File, column, Entry, out var suggestion);

        Assert.True(result);
        Assert.Equal(expectedFormCategory, suggestion.FormCategory);
        Assert.Equal(expectedRuleType, suggestion.RuleType);
        Assert.Equal(expectedValue, suggestion.Value);
        Assert.Equal(expectedIncludeSubfolders, suggestion.IncludeSubfolders);
    }

    [Theory]
    [InlineData("App", "paint.exe")]
    [InlineData("Path", @"C:\Program Files\Paint\paint.exe")]
    public void AppActivityCellDeterminesProcessRule(string column, string expectedValue)
    {
        var result = ViewLogForm.TryBuildCellRuleSuggestion(
            LogRuleCategory.App, column, Entry, out var suggestion);

        Assert.True(result);
        Assert.Equal(LogRuleCategory.App, suggestion.FormCategory);
        Assert.Equal("process", suggestion.RuleType);
        Assert.Equal(expectedValue, suggestion.Value);
    }

    [Theory]
    [InlineData("ID")]
    [InlineData("Date")]
    [InlineData("Time")]
    [InlineData("Activity")]
    [InlineData("ProcessID")]
    public void UnsupportedCellDoesNotCreateRule(string column)
    {
        Assert.False(ViewLogForm.TryBuildCellRuleSuggestion(
            LogRuleCategory.File, column, Entry, out _));
    }

    [Theory]
    [InlineData("icacls", "icacls")]
    [InlineData("icacls.exe", "icacls.exe")]
    [InlineData(@"C:\Windows\System32\icacls.exe", "icacls.exe")]
    public void FileActivityApplicationValueNormalizesForFilteredList(
        string value,
        string expected)
    {
        Assert.Equal(expected, AppSettings.NormalizeProcessName(value));
    }

    [Theory]
    [InlineData("File", LogRuleCategory.File, "file", "icacls", false)]
    [InlineData("Extension", LogRuleCategory.File, "file", "*.exe", false)]
    [InlineData("Folder", LogRuleCategory.File, "folder", @"C:\Windows\System32", true)]
    [InlineData("App", LogRuleCategory.App, "process", "icacls", false)]
    public void FileActivityGroupColumnDeterminesRuleType(
        string column,
        LogRuleCategory expectedCategory,
        string expectedRuleType,
        string expectedValue,
        bool expectedIncludeSubfolders)
    {
        var groupValue = column switch
        {
            "Extension" => ".exe",
            "Folder" => @"C:\Windows\System32",
            _ => "icacls"
        };

        var result = ViewLogForm.TryBuildGroupCellRuleSuggestion(
            LogRuleCategory.File, column, groupValue, out var suggestion);

        Assert.True(result);
        Assert.Equal(expectedCategory, suggestion.FormCategory);
        Assert.Equal(expectedRuleType, suggestion.RuleType);
        Assert.Equal(expectedValue, suggestion.Value);
        Assert.Equal(expectedIncludeSubfolders, suggestion.IncludeSubfolders);
    }
}
