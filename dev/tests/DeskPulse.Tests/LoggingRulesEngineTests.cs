using DeskPulse;
using Xunit;

namespace DeskPulse.Tests;

public sealed class LoggingRulesEngineTests
{
    [Theory]
    [InlineData(@"C:\Tools\app.exe", true)]
    [InlineData(@"C:\Tools\Suite\app.exe", true)]
    [InlineData(@"C:\Tools\Suite\Helpers\worker.exe", true)]
    [InlineData(@"C:\Other\app.exe", false)]
    public void FolderQualifiedAppWildcardMatchesOnlyInsideRequestedTree(
        string executablePath,
        bool expectedMatch)
    {
        var decision = LoggingRulesEngine.GetProgramActivityRuleDecision(
            executablePath,
            Path.GetFileName(executablePath),
            new[] { @"exclude|process||C:\Tools\**\*.exe" });

        Assert.Equal(expectedMatch ? false : null, decision);
    }

    [Theory]
    [InlineData(@"C:\Tools\One\app.exe", true)]
    [InlineData(@"C:\Tools\One\Two\app.exe", false)]
    [InlineData(@"C:\Other\One\app.exe", false)]
    public void SingleStarAppWildcardMatchesOneFolderLevel(
        string executablePath,
        bool expectedMatch)
    {
        var decision = LoggingRulesEngine.GetProgramActivityRuleDecision(
            executablePath,
            Path.GetFileName(executablePath),
            new[] { @"exclude|process||C:\Tools\*\app.exe" });

        Assert.Equal(expectedMatch ? false : null, decision);
    }

    [Fact]
    public void FilenameOnlyWildcardStillMatchesProcessName()
    {
        var decision = LoggingRulesEngine.GetProgramActivityRuleDecision(
            @"D:\Anywhere\helper-service.exe",
            "helper-service.exe",
            new[] { "exclude|process||helper-*.exe" });

        Assert.False(decision);
    }
}
