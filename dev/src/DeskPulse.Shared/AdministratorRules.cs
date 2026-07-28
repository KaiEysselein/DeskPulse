using System.Diagnostics;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DeskPulse;

/// <summary>
/// Evaluates the administrator override and shipped default machine-wide rules
/// used while Windows system activity tracking is disabled.
/// </summary>
public static class AdministratorRules
{
    private const int SupportedSchemaVersion = 1;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly object Sync = new();
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static RuleSnapshot _snapshot = CreateFallbackSnapshot();
    private static DateTime _nextRefreshUtc;
    private static FileSignature _defaultSignature;
    private static FileSignature _administratorSignature;
    private static bool _hasLoadedFiles;
    private static string _lastReportedError = "";

    internal static string? DefaultRulesFilePathOverride { get; set; }
    internal static string? AdministratorRulesFilePathOverride { get; set; }

    public static string DefaultRulesFilePath =>
        DefaultRulesFilePathOverride ?? Path.Combine(
            Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName
                ?? AppContext.BaseDirectory,
            "Config",
            "default-rules.yaml");

    public static string AdministratorRulesFilePath =>
        AdministratorRulesFilePathOverride ?? StorageLayout.AdministratorRulesFilePath;

    public static string LastLoadError { get; private set; } = "";

    public static void ReloadNow()
    {
        lock (Sync)
            _nextRefreshUtc = DateTime.MinValue;
        _ = GetSnapshot();
    }

    internal static void ResetForTests(string? defaultPath = null, string? administratorPath = null)
    {
        lock (Sync)
        {
            DefaultRulesFilePathOverride = defaultPath;
            AdministratorRulesFilePathOverride = administratorPath;
            _snapshot = CreateFallbackSnapshot();
            _nextRefreshUtc = DateTime.MinValue;
            _defaultSignature = default;
            _administratorSignature = default;
            _hasLoadedFiles = false;
            _lastReportedError = "";
            LastLoadError = "";
        }
    }

    public static IReadOnlyList<MachineWideRuleInfo> GetEffectiveRules(bool includeHidden = true) =>
        GetSnapshot().Rules
            .Where(rule => includeHidden || rule.VisibleInUi)
            .Select(rule => new MachineWideRuleInfo(
                rule.Id,
                rule.Enabled,
                rule.VisibleInUi,
                rule.Type == RuleType.Path ? "Path" : "Process",
                rule.Action == RuleAction.Include ? "Include" : "Exclude",
                rule.Value,
                rule.Source == RuleSource.Administrator ? "Administrator" : "Default",
                rule.Status,
                rule.Reason))
            .ToList();

    public static IReadOnlyList<ActivityRuleSetting> GetFileRules() =>
        GetSnapshot().Rules
            .Where(rule => rule.Enabled && rule.VisibleInUi && rule.Type == RuleType.Path)
            .Select(rule => new ActivityRuleSetting
            {
                Enabled = true,
                RuleType = "file",
                Action = rule.Action == RuleAction.Include ? "Include" : "Exclude",
                Value = rule.Value.TrimEnd(Path.DirectorySeparatorChar) + @"\**\*"
            })
            .ToList();

    public static IReadOnlyList<ActivityRuleSetting> GetProcessRules() =>
        GetSnapshot().Rules
            .Where(rule => rule.Enabled && rule.VisibleInUi && rule.Type == RuleType.Process)
            .Select(rule => new ActivityRuleSetting
            {
                Enabled = true,
                RuleType = "process",
                Action = rule.Action == RuleAction.Include ? "Include" : "Exclude",
                Value = rule.Value
            })
            .ToList();

    public static bool IsFileOrProcessExcluded(string fullPath, string processName) =>
        TryEvaluate(fullPath, processName, includePaths: true, out var excluded) && excluded;

    public static bool IsProcessExcluded(string processName) =>
        TryEvaluate("", processName, includePaths: false, out var excluded) && excluded;

    private static bool TryEvaluate(
        string fullPath,
        string processName,
        bool includePaths,
        out bool excluded)
    {
        excluded = false;
        var normalizedProcess = NormalizeProcess(processName);
        var normalizedPath = includePaths ? NormalizePath(fullPath, requireAbsolute: true) : "";

        foreach (var rule in GetSnapshot().Rules)
        {
            if (!rule.Enabled)
                continue;

            var matches = rule.Type switch
            {
                RuleType.Process => normalizedProcess.Length > 0 &&
                                    normalizedProcess.Equals(rule.Value, StringComparison.OrdinalIgnoreCase),
                RuleType.Path => includePaths && normalizedPath.Length > 0 &&
                                 (normalizedPath.Equals(rule.Value, StringComparison.OrdinalIgnoreCase) ||
                                  normalizedPath.StartsWith(
                                      rule.Value + Path.DirectorySeparatorChar,
                                      StringComparison.OrdinalIgnoreCase)),
                _ => false
            };

            if (!matches)
                continue;

            excluded = rule.Action == RuleAction.Exclude;
            return true;
        }

        if (DefaultRulesFilePathOverride == null && AdministratorRulesFilePathOverride == null)
        {
            if (normalizedProcess.Length > 0)
                RuleCandidateDiagnostics.Record(
                    includePaths ? "file-process" : "program-process",
                    normalizedProcess);
            if (includePaths && normalizedPath.Length > 0)
            {
                var extension = Path.GetExtension(normalizedPath);
                RuleCandidateDiagnostics.Record(
                    "file-extension",
                    string.IsNullOrWhiteSpace(extension) ? "[no extension]" : extension.ToLowerInvariant());
            }
        }

        return false;
    }

    private static RuleSnapshot GetSnapshot()
    {
        RefreshIfNeeded();
        // RuleSnapshot is immutable and reference assignment is atomic. Normal
        // high-volume event evaluation therefore does not take a lock.
        return _snapshot;
    }

    private static void RefreshIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now < _nextRefreshUtc)
            return;

        lock (Sync)
        {
            if (now < _nextRefreshUtc)
                return;
            _nextRefreshUtc = now.Add(RefreshInterval);

            var defaultSignature = FileSignature.Read(DefaultRulesFilePath);
            var administratorSignature = FileSignature.Read(AdministratorRulesFilePath);
            if (_hasLoadedFiles &&
                defaultSignature == _defaultSignature &&
                administratorSignature == _administratorSignature)
                return;

            try
            {
                var defaults = defaultSignature.Exists
                    ? LoadAndValidate(DefaultRulesFilePath, "default", RuleSource.Default)
                    : CreateFallbackDefinitions();
                var administrator = administratorSignature.Exists
                    ? LoadAndValidate(AdministratorRulesFilePath, "administrator", RuleSource.Administrator)
                    : new List<RuleDefinition>();

                _snapshot = BuildSnapshot(administrator, defaults);
                _defaultSignature = defaultSignature;
                _administratorSignature = administratorSignature;
                _hasLoadedFiles = true;
                LastLoadError = "";
                ReportRecoveryIfNeeded();
            }
            catch (Exception ex)
            {
                // Do not accept a partially parsed or invalid edit. The current
                // process continues using its last complete, valid snapshot.
                LastLoadError = ex.Message;
                ReportError(ex.Message);
            }
        }
    }

    private static List<RuleDefinition> LoadAndValidate(
        string path,
        string sourceName,
        RuleSource ruleSource)
    {
        RuleFile document;
        try
        {
            document = Deserializer.Deserialize<RuleFile>(File.ReadAllText(path))
                       ?? throw new InvalidDataException("The document is empty.");
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException(
                $"The {sourceName} rules file '{path}' is not valid YAML: {ex.Message}",
                ex);
        }

        if (document.Version != SupportedSchemaVersion)
            throw new InvalidDataException(
                $"The {sourceName} rules file '{path}' has unsupported version {document.Version}. Expected version {SupportedSchemaVersion}.");

        var result = new List<RuleDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in document.Rules ?? new List<RuleDocument>())
        {
            var id = (source.Id ?? "").Trim();
            if (!Regex.IsMatch(id, @"^[a-z0-9][a-z0-9._-]*$", RegexOptions.IgnoreCase))
                throw new InvalidDataException(
                    $"The {sourceName} rules file contains an invalid or missing rule id '{id}'.");
            if (!ids.Add(id))
                throw new InvalidDataException(
                    $"The {sourceName} rules file contains duplicate rule id '{id}'.");
            if (source.Revision < 1)
                throw new InvalidDataException(
                    $"Rule '{id}' in the {sourceName} rules file must have revision 1 or higher.");
            if (source.BasedOnDefaultRevision.HasValue && source.BasedOnDefaultRevision.Value < 1)
                throw new InvalidDataException(
                    $"Rule '{id}' in the {sourceName} rules file has an invalid based_on_default_revision.");

            var type = ParseType(source.Type, sourceName, id);
            var action = ParseAction(source.Action, sourceName, id);
            var values = new List<string>();
            if (!string.IsNullOrWhiteSpace(source.Value))
                values.Add(source.Value);
            if (source.Values != null)
                values.AddRange(source.Values);
            values = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            if (values.Count == 0)
                throw new InvalidDataException(
                    $"Rule '{id}' in the {sourceName} rules file has no value or values.");

            foreach (var value in values)
            {
                var normalized = type == RuleType.Path
                    ? NormalizeConfiguredPath(value, sourceName, id)
                    : NormalizeConfiguredProcess(value, sourceName, id);
                result.Add(new RuleDefinition(
                    id,
                    source.Enabled,
                    source.VisibleInUi,
                    source.Revision,
                    source.BasedOnDefaultRevision,
                    type,
                    action,
                    normalized,
                    ruleSource,
                    (source.Reason ?? "").Trim()));
            }
        }

        return result;
    }

    private static RuleSnapshot BuildSnapshot(
        IReadOnlyList<RuleDefinition> administrator,
        IReadOnlyList<RuleDefinition> defaults)
    {
        var defaultRevisions = defaults
            .GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Revision, StringComparer.OrdinalIgnoreCase);
        var annotatedAdministrator = administrator
            .Select(rule =>
            {
                if (!defaultRevisions.TryGetValue(rule.Id, out var defaultRevision))
                    return rule with { Status = "Administrator addition" };
                if (!rule.BasedOnDefaultRevision.HasValue)
                    return rule with { Status = "Overrides default; baseline not recorded" };
                return rule with
                {
                    Status = rule.BasedOnDefaultRevision.Value == defaultRevision
                        ? "Overrides current default"
                        : $"Review needed: default revision is {defaultRevision}"
                };
            })
            .ToArray();
        var administratorIds = administrator
            .Select(rule => rule.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var effective = annotatedAdministrator
            .Concat(defaults
                .Where(rule => !administratorIds.Contains(rule.Id))
                .Select(rule => rule with { Status = "Shipped default" }))
            .ToArray();
        return new RuleSnapshot(effective);
    }

    private static RuleType ParseType(string? value, string sourceName, string id) =>
        (value ?? "").Trim().ToLowerInvariant() switch
        {
            "path" => RuleType.Path,
            "process" => RuleType.Process,
            _ => throw new InvalidDataException(
                $"Rule '{id}' in the {sourceName} rules file must have type 'path' or 'process'.")
        };

    private static RuleAction ParseAction(string? value, string sourceName, string id) =>
        (value ?? "").Trim().ToLowerInvariant() switch
        {
            "include" => RuleAction.Include,
            "exclude" => RuleAction.Exclude,
            _ => throw new InvalidDataException(
                $"Rule '{id}' in the {sourceName} rules file must have action 'include' or 'exclude'.")
        };

    private static string NormalizeConfiguredPath(string value, string sourceName, string id)
    {
        var expanded = Environment.ExpandEnvironmentVariables(value.Trim());
        if (Regex.IsMatch(expanded, "%[^%]+%"))
            throw new InvalidDataException(
                $"Path rule '{id}' in the {sourceName} rules file contains an unknown environment variable.");
        var normalized = NormalizePath(expanded, requireAbsolute: true);
        if (normalized.Length == 0)
            throw new InvalidDataException(
                $"Path rule '{id}' in the {sourceName} rules file must contain a valid absolute folder path.");
        return normalized;
    }

    private static string NormalizeConfiguredProcess(string value, string sourceName, string id)
    {
        var trimmed = value.Trim();
        if (trimmed.IndexOfAny(new[] { '\\', '/', '*', '?' }) >= 0)
            throw new InvalidDataException(
                $"Process rule '{id}' in the {sourceName} rules file must be an exact executable name without a path or wildcard.");
        var normalized = NormalizeProcess(trimmed);
        if (normalized.Length == 0)
            throw new InvalidDataException(
                $"Process rule '{id}' in the {sourceName} rules file has an invalid process name.");
        return normalized;
    }

    private static string NormalizePath(string value, bool requireAbsolute)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(value.Trim());
            if (requireAbsolute && !Path.IsPathFullyQualified(expanded))
                return "";
            return Path.GetFullPath(expanded)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return "";
        }
    }

    private static string NormalizeProcess(string value) =>
        Path.GetFileNameWithoutExtension((value ?? "").Trim());

    private static RuleSnapshot CreateFallbackSnapshot() =>
        BuildSnapshot(Array.Empty<RuleDefinition>(), CreateFallbackDefinitions());

    private static List<RuleDefinition> CreateFallbackDefinitions()
    {
        var rules = new List<RuleDefinition>();
        AddFallbackPath(rules, "windows-installation-tree", @"%WINDIR%");
        AddFallbackPath(rules, "windows-error-reporting", @"%ProgramData%\Microsoft\Windows\WER");
        AddFallbackPath(rules, "windows-defender-data", @"%ProgramData%\Microsoft\Windows Defender");
        AddFallbackPath(rules, "windows-search-data", @"%ProgramData%\Microsoft\Search");
        AddFallbackPath(rules, "recycle-bin", @"%SystemDrive%\$Recycle.Bin");
        foreach (var process in new[]
                 {
                     "SearchIndexer", "SearchProtocolHost", "SearchFilterHost", "MsMpEng",
                     "CompatTelRunner", "svchost", "RuntimeBroker", "TiWorker", "TrustedInstaller",
                     "MoUsoCoreWorker", "UsoClient", "WerFault", "System"
                 })
        {
            rules.Add(new RuleDefinition(
                "windows-background-processes",
                true,
                true,
                1,
                null,
                RuleType.Process,
                RuleAction.Exclude,
                process,
                RuleSource.Default,
                "Built-in safety fallback."));
        }
        return rules;
    }

    private static void AddFallbackPath(List<RuleDefinition> rules, string id, string value)
    {
        var normalized = NormalizePath(value, requireAbsolute: true);
        if (normalized.Length > 0)
            rules.Add(new RuleDefinition(
                id,
                true,
                true,
                1,
                null,
                RuleType.Path,
                RuleAction.Exclude,
                normalized,
                RuleSource.Default,
                "Built-in safety fallback."));
    }

    private static void ReportError(string message)
    {
        if (string.Equals(message, _lastReportedError, StringComparison.Ordinal))
            return;
        _lastReportedError = message;
        Trace.TraceError(message);
        AppendDiagnostic("ERROR", message);
    }

    private static void ReportRecoveryIfNeeded()
    {
        if (_lastReportedError.Length == 0)
            return;
        AppendDiagnostic("RECOVERED", "Administrator rules were validated and the new configuration is active.");
        _lastReportedError = "";
    }

    private static void AppendDiagnostic(string level, string message)
    {
        try
        {
            StorageLayout.PrepareSystemStorage();
            File.AppendAllText(
                StorageLayout.AdministratorRulesErrorLogFilePath,
                $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
        }
        catch
        {
            // A non-elevated tray may detect the same invalid file but cannot
            // write to protected system storage. The service records it.
        }
    }

    private sealed record RuleSnapshot(IReadOnlyList<RuleDefinition> Rules);

    private sealed record RuleDefinition(
        string Id,
        bool Enabled,
        bool VisibleInUi,
        int Revision,
        int? BasedOnDefaultRevision,
        RuleType Type,
        RuleAction Action,
        string Value,
        RuleSource Source,
        string Reason,
        string Status = "");

    private enum RuleType
    {
        Path,
        Process
    }

    private enum RuleAction
    {
        Include,
        Exclude
    }

    private enum RuleSource
    {
        Default,
        Administrator
    }

    private readonly record struct FileSignature(bool Exists, long Length, DateTime LastWriteUtc)
    {
        public static FileSignature Read(string path)
        {
            try
            {
                var info = new FileInfo(path);
                return info.Exists
                    ? new FileSignature(true, info.Length, info.LastWriteTimeUtc)
                    : default;
            }
            catch
            {
                return default;
            }
        }
    }

    private sealed class RuleFile
    {
        public int Version { get; set; }
        public List<RuleDocument>? Rules { get; set; }
    }

    private sealed class RuleDocument
    {
        public string? Id { get; set; }
        public bool Enabled { get; set; } = true;
        public bool VisibleInUi { get; set; } = true;
        public int Revision { get; set; } = 1;
        public int? BasedOnDefaultRevision { get; set; }
        public string? Type { get; set; }
        public string? Action { get; set; }
        public string? Value { get; set; }
        public List<string>? Values { get; set; }
        public string? Reason { get; set; }
    }
}

public sealed record MachineWideRuleInfo(
    string Id,
    bool Enabled,
    bool VisibleInUi,
    string Type,
    string Action,
    string Value,
    string Source,
    string Status,
    string Reason);
