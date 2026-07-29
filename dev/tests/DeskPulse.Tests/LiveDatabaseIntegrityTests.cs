using DeskPulse;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeskPulse.Tests;

public sealed class LiveDatabaseIntegrityTests
{
    [Fact]
    public void InstalledDatabasesPassReadOnlyIntegrityCheck()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("DESKPULSE_LIVE_INTEGRITY"),
                "1",
                StringComparison.Ordinal))
            return;

        var databasePaths = new List<string>();
        if (File.Exists(StorageLayout.SystemDatabaseFilePath))
            databasePaths.Add(StorageLayout.SystemDatabaseFilePath);
        var currentUserDatabase = StorageLayout.GetUserDatabaseFilePath(
            StorageLayout.ResolveCurrentOrInteractiveUserSid());
        if (File.Exists(currentUserDatabase))
            databasePaths.Add(currentUserDatabase);

        Assert.NotEmpty(databasePaths);
        foreach (var path in databasePaths)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", command.ExecuteScalar()?.ToString(), ignoreCase: true);

            foreach (var table in new[] { "ActivityEvents", "ProgramEvents", "UserEvents" })
            {
                command.CommandText =
                    $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = 'ShowInCalendarView';";
                Assert.Equal(1L, command.ExecuteScalar());
            }
        }
    }

    [Fact]
    public async Task InstalledServiceCanMarkProtectedGroupedRecordsForCalendarView()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("DESKPULSE_LIVE_INTEGRITY"),
                "1",
                StringComparison.Ordinal))
            return;

        var databasePath = StorageLayout.GetUserDatabaseFilePath(
            StorageLayout.ResolveCurrentOrInteractiveUserSid());
        var originalStates = new Dictionary<long, bool>();

        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, ShowInCalendarView
                FROM ActivityEvents
                WHERE Extension = '.xlsx'
                ORDER BY Id
                LIMIT 2101;
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
                originalStates[reader.GetInt64(0)] = reader.GetInt64(1) != 0;
        }

        Assert.NotEmpty(originalStates);
        try
        {
            Assert.Equal(
                originalStates.Count,
                await ServicePipeClient.SetCalendarVisibilityAsync(
                    "ActivityEvents",
                    originalStates.Keys.ToArray(),
                    showInCalendar: true));

            using var verify = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            verify.Open();
            using var command = verify.CreateCommand();
            command.CommandText =
                $"SELECT COUNT(*) FROM ActivityEvents WHERE Id IN ({string.Join(",", originalStates.Keys)}) AND ShowInCalendarView <> 0;";
            Assert.Equal((long)originalStates.Count, command.ExecuteScalar());
        }
        finally
        {
            foreach (var stateGroup in originalStates.GroupBy(item => item.Value))
            {
                await ServicePipeClient.SetCalendarVisibilityAsync(
                    "ActivityEvents",
                    stateGroup.Select(item => item.Key).ToArray(),
                    stateGroup.Key);
            }
        }
    }
}
