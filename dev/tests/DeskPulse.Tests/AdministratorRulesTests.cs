using DeskPulse;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeskPulse.Tests;

public sealed class AdministratorRulesTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "DeskPulse.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly string _defaults;
    private readonly string _administrator;

    public AdministratorRulesTests()
    {
        Directory.CreateDirectory(_folder);
        _defaults = Path.Combine(_folder, "default-rules.yaml");
        _administrator = Path.Combine(_folder, "admin-rules.yaml");
    }

    [Fact]
    public void AdministratorRuleWithSameIdOverridesDefault()
    {
        WriteDefaults("""
            version: 1
            rules:
              - id: test-path
                revision: 1
                enabled: true
                visible_in_ui: true
                type: path
                action: exclude
                value: "C:\\PolicyRoot"
                reason: "Default"
            """);
        WriteAdministrator("""
            version: 1
            rules:
              - id: test-path
                based_on_default_revision: 1
                enabled: true
                visible_in_ui: true
                type: path
                action: include
                value: "C:\\PolicyRoot"
                reason: "Required exception"
            """);

        Reload();

        Assert.False(AdministratorRules.IsFileOrProcessExcluded(@"C:\PolicyRoot\file.txt", ""));
        var rule = Assert.Single(AdministratorRules.GetEffectiveRules());
        Assert.Equal("Administrator", rule.Source);
        Assert.Equal("Overrides current default", rule.Status);
    }

    [Fact]
    public void HiddenRuleIsEnforcedButOmittedFromOrdinaryRuleRows()
    {
        WriteDefaults("""
            version: 1
            rules:
              - id: hidden-process
                revision: 1
                enabled: true
                visible_in_ui: false
                type: process
                action: exclude
                value: HiddenWorker.exe
                reason: "Noise"
            """);
        WriteAdministrator("version: 1\nrules: []\n");

        Reload();

        Assert.True(AdministratorRules.IsProcessExcluded("HiddenWorker"));
        Assert.Empty(AdministratorRules.GetProcessRules());
        Assert.Single(AdministratorRules.GetEffectiveRules(includeHidden: true));
    }

    [Fact]
    public void InvalidEditRetainsLastValidSnapshot()
    {
        WriteDefaults("""
            version: 1
            rules:
              - id: valid-process
                revision: 1
                enabled: true
                visible_in_ui: true
                type: process
                action: exclude
                value: ValidWorker
                reason: "Test"
            """);
        WriteAdministrator("version: 1\nrules: []\n");
        Reload();
        Assert.True(AdministratorRules.IsProcessExcluded("ValidWorker"));

        WriteAdministrator("version: 1\nrules:\n  - id: broken\n    type: unknown\n");
        AdministratorRules.ReloadNow();

        Assert.True(AdministratorRules.IsProcessExcluded("ValidWorker"));
        Assert.NotEmpty(AdministratorRules.LastLoadError);
    }

    [Fact]
    public void MissingDefaultFileUsesSafetyFallback()
    {
        WriteAdministrator("version: 1\nrules: []\n");

        Reload();

        Assert.Equal(
            MachineWideRuleAction.RouteSystem,
            AdministratorRules.GetRoutingAction("", "SearchIndexer.exe"));
        Assert.Contains(
            AdministratorRules.GetEffectiveRules(),
            rule => rule.Id == "windows-installation-tree");
    }

    [Fact]
    public void RoutingRuleTakesSingleExplicitDestination()
    {
        WriteDefaults("""
            version: 1
            rules:
              - id: route-windows
                revision: 1
                enabled: true
                visible_in_ui: true
                type: path
                action: route_system
                owner_scope: system
                value: "C:\\Windows"
                reason: "System-owned activity"
            """);
        WriteAdministrator("version: 1\nrules: []\n");

        Reload();

        Assert.Equal(
            MachineWideRuleAction.RouteSystem,
            AdministratorRules.GetRoutingAction(@"C:\Windows\System32\driver.sys", "System", EventScope.System));
        Assert.Equal(
            MachineWideRuleAction.None,
            AdministratorRules.GetRoutingAction(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", "powershell", EventScope.User));
        Assert.False(AdministratorRules.IsFileOrProcessExcluded(
            @"C:\Windows\System32\driver.sys",
            "System"));
    }

    [Fact]
    public void HistoricalPreviewUsesRulesBeforeStoredUserSid()
    {
        WriteDefaults("""
            version: 1
            rules:
              - id: route-service
                revision: 1
                enabled: true
                visible_in_ui: true
                type: process
                action: route_system
                value: ServiceWorker
                reason: "System service"
            """);
        WriteAdministrator("version: 1\nrules: []\n");
        Reload();

        var result = HistoricalAttributionPreview.Classify(
            EventScope.User,
            EventScope.User,
            "S-1-5-21-100-200-300-1001",
            123,
            "ServiceWorker",
            @"C:\Data\file.txt");

        Assert.Equal(EventScope.System, result.Target);
        Assert.Equal("High", result.Confidence);
    }

    [Fact]
    public void HistoricalPreviewReadsWithoutMutatingSourceDatabase()
    {
        WriteDefaults("version: 1\nrules: []\n");
        WriteAdministrator("version: 1\nrules: []\n");
        Reload();
        var databasePath = Path.Combine(_folder, "preview.db");
        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE ActivityEvents
                (
                    Scope TEXT,
                    WindowsSid TEXT,
                    ProcessId INTEGER,
                    ProcessName TEXT,
                    FullPath TEXT
                );
                CREATE TABLE ProgramEvents
                (
                    Scope TEXT,
                    WindowsSid TEXT,
                    ProcessId INTEGER,
                    ProgramName TEXT,
                    FilePath TEXT
                );
                INSERT INTO ActivityEvents VALUES
                    ('User', 'S-1-5-21-100-200-300-1001', 42, 'Worker', 'C:\Data\file.txt');
                """;
            command.ExecuteNonQuery();
        }

        var reportPath = Path.Combine(_folder, "preview.csv");
        var result = HistoricalAttributionPreview.Generate(new[] { databasePath }, reportPath);

        Assert.Equal(1, result.RecordsExamined);
        Assert.True(File.Exists(reportPath));
        using var verify = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        verify.Open();
        using var count = verify.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM ActivityEvents;";
        Assert.Equal(1L, (long)count.ExecuteScalar()!);
    }

    [Fact]
    public void ChangedDefaultRevisionFlagsOverrideForReview()
    {
        WriteDefaults("""
            version: 1
            rules:
              - id: changed-default
                revision: 2
                enabled: true
                visible_in_ui: true
                type: process
                action: exclude
                value: NewDefault
                reason: "Changed"
            """);
        WriteAdministrator("""
            version: 1
            rules:
              - id: changed-default
                based_on_default_revision: 1
                enabled: true
                visible_in_ui: true
                type: process
                action: exclude
                value: OldOverride
                reason: "Old"
            """);

        Reload();

        Assert.StartsWith(
            "Review needed",
            Assert.Single(AdministratorRules.GetEffectiveRules()).Status);
    }

    [Fact]
    public void RemovingDisabledOverrideRestoresDefault()
    {
        WriteDefaults("""
            version: 1
            rules:
              - id: restorable-process
                revision: 1
                enabled: true
                visible_in_ui: true
                type: process
                action: exclude
                value: RestorableWorker
                reason: "Default"
            """);
        WriteAdministrator("""
            version: 1
            rules:
              - id: restorable-process
                based_on_default_revision: 1
                enabled: false
                visible_in_ui: true
                type: process
                action: exclude
                value: RestorableWorker
                reason: "Temporarily disabled"
            """);
        Reload();
        Assert.False(AdministratorRules.IsProcessExcluded("RestorableWorker"));

        WriteAdministrator("version: 1\nrules: []\n");
        AdministratorRules.ReloadNow();

        Assert.True(AdministratorRules.IsProcessExcluded("RestorableWorker"));
    }

    private void Reload()
    {
        AdministratorRules.ResetForTests(_defaults, _administrator);
        AdministratorRules.ReloadNow();
        Assert.True(string.IsNullOrEmpty(AdministratorRules.LastLoadError), AdministratorRules.LastLoadError);
    }

    private void WriteDefaults(string content) => File.WriteAllText(_defaults, content);
    private void WriteAdministrator(string content) => File.WriteAllText(_administrator, content);

    public void Dispose()
    {
        AdministratorRules.ResetForTests();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch
        {
            // Temporary test cleanup is best effort.
        }
    }
}
