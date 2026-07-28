using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace DeskPulse;

/// <summary>
/// Produces bounded, aggregate diagnostics for activity not matched by a
/// machine-wide rule. It intentionally stores no paths, user names, SIDs, or
/// event contents.
/// </summary>
internal static class RuleCandidateDiagnostics
{
    private const int MaximumKeys = 500;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, long> Counts =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object FlushSync = new();
    private static DateTime _nextFlushUtc = DateTime.UtcNow.Add(FlushInterval);

    public static void Record(string category, string value)
    {
        var safeCategory = Sanitize(category, 40);
        var safeValue = Sanitize(value, 180);
        if (safeCategory.Length == 0 || safeValue.Length == 0)
            return;

        var key = safeCategory + "\t" + safeValue;
        if (Counts.Count < MaximumKeys || Counts.ContainsKey(key))
            Counts.AddOrUpdate(key, 1, (_, count) => count + 1);
        else
            Counts.AddOrUpdate("other\t[additional values]", 1, (_, count) => count + 1);

        if (DateTime.UtcNow >= _nextFlushUtc)
            Flush();
    }

    private static void Flush()
    {
        lock (FlushSync)
        {
            var now = DateTime.UtcNow;
            if (now < _nextFlushUtc)
                return;
            _nextFlushUtc = now.Add(FlushInterval);
            if (Counts.IsEmpty)
                return;

            var snapshot = Counts
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var output = new StringBuilder();
            if (!File.Exists(StorageLayout.RuleCandidateDiagnosticsFilePath))
                output.AppendLine("captured_at,category,value,count");
            var capturedAt = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            foreach (var item in snapshot)
            {
                var parts = item.Key.Split('\t', 2);
                output.Append(Csv(capturedAt)).Append(',')
                    .Append(Csv(parts[0])).Append(',')
                    .Append(Csv(parts.Length > 1 ? parts[1] : "")).Append(',')
                    .Append(item.Value.ToString(CultureInfo.InvariantCulture))
                    .AppendLine();
            }

            try
            {
                StorageLayout.PrepareSystemStorage();
                File.AppendAllText(StorageLayout.RuleCandidateDiagnosticsFilePath, output.ToString());
                foreach (var item in snapshot)
                    Counts.TryRemove(new KeyValuePair<string, long>(item.Key, item.Value));
            }
            catch
            {
                // Retain counts and retry later if protected storage is temporarily unavailable.
            }
        }
    }

    private static string Sanitize(string value, int maximumLength)
    {
        var normalized = new string((value ?? "")
            .Where(character => !char.IsControl(character))
            .ToArray())
            .Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
