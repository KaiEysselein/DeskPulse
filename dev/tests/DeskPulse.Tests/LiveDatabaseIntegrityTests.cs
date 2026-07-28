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
        if (Directory.Exists(StorageLayout.UsersFolder))
        {
            databasePaths.AddRange(Directory.EnumerateFiles(
                StorageLayout.UsersFolder,
                "DeskPulse.db",
                SearchOption.AllDirectories));
        }

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
        }
    }
}
