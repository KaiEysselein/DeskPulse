using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DeskPulse;

internal enum AppIconState
{
    Normal,
    Paused,
    Warning
}

internal static class AppIcon
{
    public static Icon Load(AppIconState state = AppIconState.Normal)
    {
        try
        {
            var fileName = state switch
            {
                AppIconState.Paused => "DeskPulse_Paused.ico",
                AppIconState.Warning => "DeskPulse_Warning.ico",
                _ => "DeskPulse_Normal.ico"
            };

            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Resources", fileName),
                Path.Combine(Application.StartupPath, "Resources", fileName)
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return new Icon(candidate);
            }

            if (state == AppIconState.Normal)
            {
                var executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (executableIcon != null)
                    return (Icon)executableIcon.Clone();
            }
        }
        catch
        {
            // Fall back to the executable or Windows application icon below.
        }

        var fallback = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        return fallback != null ? (Icon)fallback.Clone() : (Icon)SystemIcons.Application.Clone();
    }

    public static void Apply(Form form)
    {
        form.ShowIcon = true;
        form.Icon = Load(AppIconState.Normal);
        EnableScrolling(form);
    }

    public static void EnableScrolling(Form form)
    {
        // Preserve the form's designed content extent. If Windows display scaling
        // or a compact screen makes the client area smaller, scrollbars expose
        // controls that would otherwise be clipped.
        form.AutoScroll = true;
        form.AutoScrollMinSize = form.ClientSize;
        EnableTabPageScrolling(form);
    }

    private static void EnableTabPageScrolling(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is TabPage page)
                page.AutoScroll = true;
            if (child.HasChildren)
                EnableTabPageScrolling(child);
        }
    }
}
