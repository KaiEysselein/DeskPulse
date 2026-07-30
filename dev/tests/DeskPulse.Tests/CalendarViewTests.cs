using DeskPulse;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeskPulse.Tests;

public sealed class CalendarViewTests
{
    [Fact]
    public void CalendarQueryReturnsFileAppAndUserActivity()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DeskPulse.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(folder, "calendar-sources.db");

        try
        {
            using (var database = new DeskPulseDatabase(databasePath))
                database.Initialize();

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using (var insert = connection.CreateCommand())
            {
                insert.CommandText =
                    """
                    INSERT INTO ActivityEvents (CreatedAt, ActivityType, Item, FileName, FullPath)
                    VALUES ('2026-07-30 08:00:00.000', 'Opened', 'report.docx', 'report.docx', 'C:\Docs\report.docx');
                    INSERT INTO ProgramEvents (CreatedAt, EventDescription, ProgramName, WindowTitle)
                    VALUES ('2026-07-30 09:00:00.000', 'Started', 'Word', 'Quarterly report');
                    INSERT INTO UserEvents (CreatedAt, EventDescription, UserName)
                    VALUES ('2026-07-30 10:00:00.000', 'SessionUnlocked', 'Kai');
                    """;
                insert.ExecuteNonQuery();
            }

            using var command = connection.CreateCommand();
            command.CommandText = CalendarViewForm.BuildEntriesQuery(markedRecordsOnly: false);
            using var reader = command.ExecuteReader();
            var activityTypes = new List<string>();
            while (reader.Read())
                activityTypes.Add(reader.GetString(1));

            Assert.Equal(
                new[] { "User Activity", "App Activity", "File Activity" },
                activityTypes);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void DetailsHeaderGroupsAndUngroups()
    {
        Assert.Equal("Details", CalendarViewForm.GetHeaderGrouping("Details"));
        Assert.Equal("Details", CalendarViewForm.ToggleHeaderGrouping("Item", "Details"));
        Assert.Equal("None", CalendarViewForm.ToggleHeaderGrouping("Details", "Details"));
    }

    [Fact]
    public void InitializeAddsCalendarFlagToEveryActivityTable()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DeskPulse.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(folder, "calendar.db");

        try
        {
            using (var database = new DeskPulseDatabase(databasePath))
                database.Initialize();

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            foreach (var table in new[] { "ActivityEvents", "ProgramEvents", "UserEvents" })
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA table_info({table});";
                using var reader = command.ExecuteReader();
                var columns = new List<string>();
                while (reader.Read())
                    columns.Add(reader.GetString(1));

                Assert.Contains("ShowInCalendarView", columns);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ExistingDatabaseReceivesCalendarFlagWithUncheckedDefault()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DeskPulse.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(folder, "calendar-migration.db");
        Directory.CreateDirectory(folder);

        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var create = connection.CreateCommand();
                create.CommandText =
                    """
                    CREATE TABLE ActivityEvents
                    (
                        Id INTEGER PRIMARY KEY,
                        CreatedAt TEXT NOT NULL,
                        ActivityType TEXT NOT NULL,
                        Item TEXT NOT NULL
                    );
                    INSERT INTO ActivityEvents (Id, CreatedAt, ActivityType, Item)
                    VALUES (1, '2026-07-29 10:00:00.000', 'Opened', 'example.docx');
                    """;
                create.ExecuteNonQuery();
            }

            using (var database = new DeskPulseDatabase(databasePath))
                database.Initialize();

            using var verify = new SqliteConnection($"Data Source={databasePath}");
            verify.Open();
            using var command = verify.CreateCommand();
            command.CommandText = "SELECT ShowInCalendarView FROM ActivityEvents WHERE Id = 1;";

            Assert.Equal(0L, command.ExecuteScalar());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ServiceDatabaseOperationUpdatesOnlyRequestedCalendarRecords()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DeskPulse.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(folder, "calendar-update.db");

        try
        {
            using (var database = new DeskPulseDatabase(databasePath))
            {
                database.Initialize();
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    connection.Open();
                    using var insert = connection.CreateCommand();
                    insert.CommandText =
                        """
                        INSERT INTO ActivityEvents (CreatedAt, ActivityType, Item)
                        VALUES ('2026-07-29 10:00:00.000', 'Opened', 'one.docx'),
                               ('2026-07-29 11:00:00.000', 'Opened', 'two.docx'),
                               ('2026-07-29 12:00:00.000', 'Opened', 'three.txt');
                        """;
                    insert.ExecuteNonQuery();
                }

                Assert.Equal(2, database.SetCalendarVisibilityByIds(
                    "ActivityEvents",
                    new long[] { 1, 2 },
                    showInCalendar: true));
            }

            using var verify = new SqliteConnection($"Data Source={databasePath}");
            verify.Open();
            using var command = verify.CreateCommand();
            command.CommandText =
                "SELECT Id, ShowInCalendarView FROM ActivityEvents ORDER BY Id;";
            using var reader = command.ExecuteReader();
            var values = new List<(long Id, long Calendar)>();
            while (reader.Read())
                values.Add((reader.GetInt64(0), reader.GetInt64(1)));

            Assert.Equal(
                new[] { (1L, 1L), (2L, 1L), (3L, 0L) },
                values);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
