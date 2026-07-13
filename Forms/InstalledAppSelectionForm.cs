using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeskPulse;

public partial class InstalledAppSelectionForm : Form
{
    private readonly List<InstalledApplicationInfo> _allApplications = new();

    public InstalledAppSelectionForm()
    {
        InitializeComponent();
        AppIcon.Apply(this);
        Load += InstalledAppSelectionForm_Load;
    }

    public IReadOnlyList<string> SelectedExecutablePaths { get; private set; } = Array.Empty<string>();

    private void InstalledAppSelectionForm_Load(object? sender, EventArgs e)
    {
        try
        {
            _allApplications.Clear();
            _allApplications.AddRange(InstalledApplicationDiscovery.FindApplications());
            ApplyFilter();
            _searchTextBox.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "The installed application list could not be read.\n\n" + ex.Message,
                "DeskPulse",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var checkedPaths = _applicationList.CheckedItems
            .OfType<InstalledApplicationInfo>()
            .Select(item => item.ExecutablePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filter = _searchTextBox.Text.Trim();
        var visible = string.IsNullOrWhiteSpace(filter)
            ? _allApplications
            : _allApplications.Where(item =>
                item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.ExecutableName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.ExecutablePath.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        _applicationList.BeginUpdate();
        try
        {
            _applicationList.Items.Clear();
            foreach (var item in visible)
                _applicationList.Items.Add(item, checkedPaths.Contains(item.ExecutablePath));
        }
        finally
        {
            _applicationList.EndUpdate();
        }

        _resultCountLabel.Text = $"{visible.Count:N0} applications with a resolvable executable";
    }

    private void AddSelectedButton_Click(object? sender, EventArgs e)
    {
        var selected = _applicationList.CheckedItems
            .OfType<InstalledApplicationInfo>()
            .Select(item => item.ExecutablePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one application.",
                "DeskPulse",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        SelectedExecutablePaths = selected;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ApplicationList_DoubleClick(object? sender, EventArgs e)
    {
        if (_applicationList.SelectedIndex < 0)
            return;

        var index = _applicationList.SelectedIndex;
        _applicationList.SetItemChecked(index, true);
        AddSelectedButton_Click(sender, e);
    }
}

internal sealed class InstalledApplicationInfo
{
    public string DisplayName { get; init; } = "";
    public string ExecutablePath { get; init; } = "";
    public string ExecutableName => Path.GetFileName(ExecutablePath);

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(ExecutableName)
            ? DisplayName
            : $"{DisplayName}    [{ExecutableName}]";
    }
}

internal static class InstalledApplicationDiscovery
{
    private static readonly string[] UninstallRegistryPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    private const string AppPathsRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

    public static List<InstalledApplicationInfo> FindApplications()
    {
        var applications = new Dictionary<string, InstalledApplicationInfo>(StringComparer.OrdinalIgnoreCase);

        ReadUninstallEntries(Registry.CurrentUser, applications);
        ReadUninstallEntries(Registry.LocalMachine, applications);
        ReadAppPaths(Registry.CurrentUser, applications);
        ReadAppPaths(Registry.LocalMachine, applications);

        return applications.Values
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.ExecutableName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ReadUninstallEntries(RegistryKey root, IDictionary<string, InstalledApplicationInfo> applications)
    {
        foreach (var registryPath in UninstallRegistryPaths)
        {
            try
            {
                using var uninstallKey = root.OpenSubKey(registryPath);
                if (uninstallKey == null)
                    continue;

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = uninstallKey.OpenSubKey(subKeyName);
                        if (appKey == null)
                            continue;

                        var displayName = Convert.ToString(appKey.GetValue("DisplayName"))?.Trim() ?? "";
                        var displayIcon = Convert.ToString(appKey.GetValue("DisplayIcon"))?.Trim() ?? "";
                        var executablePath = NormalizeExecutablePath(displayIcon);

                        if (displayName.Length == 0 || executablePath.Length == 0)
                            continue;

                        AddApplication(applications, displayName, executablePath);
                    }
                    catch
                    {
                        // Ignore unreadable individual application entries.
                    }
                }
            }
            catch
            {
                // Ignore registry locations that are unavailable on this system.
            }
        }
    }

    private static void ReadAppPaths(RegistryKey root, IDictionary<string, InstalledApplicationInfo> applications)
    {
        try
        {
            using var appPathsKey = root.OpenSubKey(AppPathsRegistryPath);
            if (appPathsKey == null)
                return;

            foreach (var subKeyName in appPathsKey.GetSubKeyNames())
            {
                try
                {
                    using var appKey = appPathsKey.OpenSubKey(subKeyName);
                    if (appKey == null)
                        continue;

                    var executablePath = NormalizeExecutablePath(Convert.ToString(appKey.GetValue(null)) ?? "");
                    if (executablePath.Length == 0)
                        continue;

                    var displayName = Path.GetFileNameWithoutExtension(subKeyName);
                    AddApplication(applications, displayName, executablePath);
                }
                catch
                {
                    // Ignore unreadable individual App Paths entries.
                }
            }
        }
        catch
        {
            // Ignore registry locations that are unavailable on this system.
        }
    }

    private static void AddApplication(
        IDictionary<string, InstalledApplicationInfo> applications,
        string displayName,
        string executablePath)
    {
        var normalizedPath = Environment.ExpandEnvironmentVariables(executablePath.Trim().Trim('"'));
        if (!normalizedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return;

        if (!Path.IsPathRooted(normalizedPath))
            return;

        applications.TryAdd(normalizedPath, new InstalledApplicationInfo
        {
            DisplayName = displayName,
            ExecutablePath = normalizedPath
        });
    }

    private static string NormalizeExecutablePath(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return "";

        var value = Environment.ExpandEnvironmentVariables(rawValue.Trim());

        // DisplayIcon commonly ends with an icon index, for example app.exe,0.
        var commaIndex = value.LastIndexOf(',');
        if (commaIndex > 0 && int.TryParse(value[(commaIndex + 1)..].Trim(), out _))
            value = value[..commaIndex];

        value = value.Trim().Trim('"');

        if (!value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return "";

        return value;
    }
}
