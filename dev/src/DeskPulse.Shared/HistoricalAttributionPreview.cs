using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace DeskPulse;

public static class HistoricalAttributionPreview
{
    public static AttributionPreviewResult Generate(
        IEnumerable<string> databasePaths,
        string outputPath)
    {
        var aggregates = new Dictionary<PreviewKey, long>();
        long examined = 0;
        foreach (var path in databasePaths
                     .Where(File.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var source = StorageLayout.TryGetUserSidFromDatabaseFilePath(path, out _)
                ? EventScope.User
                : EventScope.System;
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            }.ToString());
            connection.Open();
            foreach (var table in new[] { "ActivityEvents", "ProgramEvents" })
            {
                using var command = connection.CreateCommand();
                command.CommandText = table == "ActivityEvents"
                    ? "SELECT COALESCE(Scope,''), COALESCE(WindowsSid,''), COALESCE(ProcessId,0), COALESCE(ProcessName,''), COALESCE(FullPath,'') FROM ActivityEvents;"
                    : "SELECT COALESCE(Scope,''), COALESCE(WindowsSid,''), COALESCE(ProcessId,0), COALESCE(ProgramName,''), COALESCE(FilePath,'') FROM ProgramEvents;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    examined++;
                    var storedScope = reader.GetString(0);
                    var sid = reader.GetString(1);
                    var processId = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture);
                    var processName = reader.GetString(3);
                    var pathValue = reader.GetString(4);
                    var classification = Classify(source, storedScope, sid, processId, processName, pathValue);
                    var key = new PreviewKey(
                        Path.GetFileName(path),
                        table,
                        source,
                        classification.Target,
                        classification.Confidence,
                        classification.Reason,
                        processName);
                    aggregates.TryGetValue(key, out var count);
                    aggregates[key] = count + 1;
                }
            }
        }

        var csv = new StringBuilder("database,table,current_scope,proposed_scope,confidence,reason,process,count\r\n");
        foreach (var item in aggregates
                     .OrderByDescending(item => item.Value)
                     .ThenBy(item => item.Key.Process, StringComparer.OrdinalIgnoreCase))
        {
            csv.Append(Csv(item.Key.Database)).Append(',')
                .Append(Csv(item.Key.Table)).Append(',')
                .Append(Csv(item.Key.Current)).Append(',')
                .Append(Csv(item.Key.Proposed)).Append(',')
                .Append(Csv(item.Key.Confidence)).Append(',')
                .Append(Csv(item.Key.Reason)).Append(',')
                .Append(Csv(item.Key.Process)).Append(',')
                .Append(item.Value.ToString(CultureInfo.InvariantCulture))
                .Append("\r\n");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, csv.ToString(), new UTF8Encoding(false));

        var proposedMoves = aggregates
            .Where(item => !item.Key.Current.Equals(item.Key.Proposed, StringComparison.OrdinalIgnoreCase) &&
                           item.Key.Proposed is EventScope.System or EventScope.User)
            .Sum(item => item.Value);
        var unresolved = aggregates
            .Where(item => item.Key.Proposed.Equals("Unresolved", StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Value);
        return new AttributionPreviewResult(examined, proposedMoves, unresolved, outputPath);
    }

    internal static HistoricalClassification Classify(
        string databaseScope,
        string storedScope,
        string windowsSid,
        int processId,
        string processName,
        string path)
    {
        var ownerScope = !string.IsNullOrWhiteSpace(storedScope) ? storedScope : databaseScope;
        var action = AdministratorRules.GetRoutingAction(path, processName, ownerScope);
        if (action == MachineWideRuleAction.RouteSystem)
            return new HistoricalClassification(EventScope.System, "High", "Explicit route_system rule");
        if (action == MachineWideRuleAction.RouteUser)
            return new HistoricalClassification(EventScope.User, "High", "Explicit route_user rule");
        if (action == MachineWideRuleAction.Exclude)
            return new HistoricalClassification("Excluded", "High", "Explicit exclusion rule");
        if (processId == 4 || processName.Equals("System", StringComparison.OrdinalIgnoreCase) ||
            ProcessOwnerAttribution.IsSystemIdentity(windowsSid))
            return new HistoricalClassification(EventScope.System, "High", "Stored system identity");
        if (!string.IsNullOrWhiteSpace(windowsSid))
            return new HistoricalClassification(EventScope.User, "High", "Stored user SID");
        if (!string.IsNullOrWhiteSpace(storedScope))
            return new HistoricalClassification(storedScope, "Low", "Stored scope only; owner unavailable");
        return new HistoricalClassification(
            string.IsNullOrWhiteSpace(databaseScope) ? "Unresolved" : databaseScope,
            "Low",
            "Database location only; owner unavailable");
    }

    private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

    private sealed record PreviewKey(
        string Database,
        string Table,
        string Current,
        string Proposed,
        string Confidence,
        string Reason,
        string Process);
}

public sealed record AttributionPreviewResult(
    long RecordsExamined,
    long ProposedMoves,
    long UnresolvedRecords,
    string OutputPath);

internal sealed record HistoricalClassification(string Target, string Confidence, string Reason);
