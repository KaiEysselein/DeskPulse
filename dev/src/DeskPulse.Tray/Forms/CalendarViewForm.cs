#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;

namespace DeskPulse;

public sealed class CalendarViewForm : Form
{
    private readonly string _connectionString;
    private readonly MonthCalendar _calendar = new() { MaxSelectionCount = 1, ShowTodayCircle = true };
    private readonly CheckBox _showAll = new() { Text = "Show all marked records", Checked = true, AutoSize = true };
    private readonly Label _status = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly DataGridView _grid = new();
    private List<CalendarEntry> _entries = new();

    public CalendarViewForm(
        string databaseFilePath,
        string windowTitle = "DeskPulse - Calendar View")
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        Text = windowTitle;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1040, 620);
        MinimumSize = new Size(760, 480);
        WindowState = FormWindowState.Maximized;
        AppIcon.Apply(this);

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(12, 10, 12, 4),
            Text = "Calendar View shows records marked with the Calendar checkbox in the activity log."
        };

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(12, 8, 8, 12) };
        _calendar.Dock = DockStyle.Top;
        _showAll.Top = 190;
        _showAll.Left = 12;
        _status.Top = 222;
        _status.Left = 12;
        _status.Width = 230;
        _status.Height = 72;
        leftPanel.Controls.Add(_calendar);
        leftPanel.Controls.Add(_showAll);
        leftPanel.Controls.Add(_status);

        ConfigureGrid();
        Controls.Add(_grid);
        Controls.Add(leftPanel);
        Controls.Add(header);

        _calendar.DateSelected += (_, e) =>
        {
            _showAll.Checked = false;
            ShowEntries(e.Start.Date);
        };
        _showAll.CheckedChanged += (_, _) =>
        {
            if (_showAll.Checked)
                ShowEntries(null);
            else
                ShowEntries(_calendar.SelectionStart.Date);
        };
        Activated += (_, _) => LoadCalendarEntries();
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToOrderColumns = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 72 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Time", FillWeight = 65 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ActivityType", HeaderText = "Activity", FillWeight = 85 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "Item", FillWeight = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Details", HeaderText = "Details", FillWeight = 210 });
    }

    private void LoadCalendarEntries()
    {
        try
        {
            _entries.Clear();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT CreatedAt, 'File Activity', COALESCE(NULLIF(FileName, ''), NULLIF(FullPath, ''), Item), COALESCE(FullPath, '')
                FROM ActivityEvents
                WHERE ShowInCalendarView <> 0
                UNION ALL
                SELECT CreatedAt, 'App Activity', COALESCE(ProgramName, ''), COALESCE(FilePath, '')
                FROM ProgramEvents
                WHERE ShowInCalendarView <> 0
                UNION ALL
                SELECT CreatedAt, 'User Activity', COALESCE(EventDescription, ''), COALESCE(UserName, '')
                FROM UserEvents
                WHERE ShowInCalendarView <> 0
                ORDER BY CreatedAt DESC;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var createdAtText = reader.IsDBNull(0) ? "" : reader.GetString(0);
                if (!DateTime.TryParse(createdAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var createdAt) &&
                    !DateTime.TryParse(createdAtText, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out createdAt))
                {
                    continue;
                }

                _entries.Add(new CalendarEntry(
                    createdAt,
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3)));
            }

            var markedDates = _entries.Select(entry => entry.CreatedAt.Date).Distinct().ToArray();
            _calendar.BoldedDates = markedDates;
            if (_entries.Count > 0)
                _calendar.SetDate(_entries[0].CreatedAt.Date);
            ShowEntries(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "DeskPulse could not open Calendar View.\n\n" + ex.Message,
                "Calendar View",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ShowEntries(DateTime? selectedDate)
    {
        var visibleEntries = selectedDate.HasValue
            ? _entries.Where(entry => entry.CreatedAt.Date == selectedDate.Value.Date)
            : _entries;

        _grid.Rows.Clear();
        var count = 0;
        foreach (var entry in visibleEntries)
        {
            _grid.Rows.Add(
                entry.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                entry.CreatedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                entry.ActivityType,
                entry.Subject,
                entry.Details);
            count++;
        }

        _status.Text = selectedDate.HasValue
            ? $"{count:N0} marked record(s) on {selectedDate.Value:dd MMMM yyyy}."
            : $"{count:N0} marked record(s) across all dates.\r\nBold dates contain marked records.";
        _grid.ClearSelection();
    }

    private sealed record CalendarEntry(DateTime CreatedAt, string ActivityType, string Subject, string Details);
}
