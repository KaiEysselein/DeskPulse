using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace DeskPulse;

public sealed partial class AboutForm : Form
{
    public AboutForm()
    {
        InitializeComponent();
        AppIcon.Apply(this);

        titleLabel.Text = AppInfo.AppName;
        versionLabel.Text = "Version " + AppInfo.Version;
        projectLinkLabel.Text = AppInfo.GitHubUrl;
        projectLinkLabel.Links.Add(0, AppInfo.GitHubUrl.Length, AppInfo.GitHubUrl);
        projectLinkLabel.LinkClicked += OnProjectLinkClicked;
    }

    private static void OnProjectLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link?.LinkData is not string url)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
