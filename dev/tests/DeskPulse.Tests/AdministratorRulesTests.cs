using DeskPulse;
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

        Assert.True(AdministratorRules.IsProcessExcluded("SearchIndexer.exe"));
        Assert.Contains(
            AdministratorRules.GetEffectiveRules(),
            rule => rule.Id == "windows-installation-tree");
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
