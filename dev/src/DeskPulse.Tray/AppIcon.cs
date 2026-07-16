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
    }
}
