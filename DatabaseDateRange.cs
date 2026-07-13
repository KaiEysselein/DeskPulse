#nullable enable

using System;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DeskPulse;

internal static class DatabaseDateRange
{
    public static DateTime GetFirstRecordedDate(string databaseFilePath)
    {
        if (string.IsNullOrWhiteSpace(databaseFilePath) || !System.IO.File.Exists(databaseFilePath))
            return DateTime.Today;

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databaseFilePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT MIN(CreatedAt)
                FROM (
                    SELECT CreatedAt FROM ActivityEvents
                    UNION ALL
                    SELECT CreatedAt FROM ProgramEvents
                    UNION ALL
                    SELECT CreatedAt FROM UserEvents
                )
                WHERE CreatedAt IS NOT NULL AND TRIM(CreatedAt) <> '';
                """;

            var value = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed) ||
                DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                var firstDate = parsed.Date;
                return firstDate > DateTime.Today ? DateTime.Today : firstDate;
            }
        }
        catch
        {
            // A report must still open when the database is new, unavailable, or from an older schema.
        }

        return DateTime.Today;
    }
}
