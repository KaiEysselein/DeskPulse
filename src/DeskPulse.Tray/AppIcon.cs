using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DeskPulse;

internal static class AppIcon
{
    public static Icon Load()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "DeskPulse.ico"),
                Path.Combine(Application.StartupPath, "DeskPulse.ico")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return new Icon(candidate);
            }

            var executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (executableIcon != null)
                return (Icon)executableIcon.Clone();
        }
        catch
        {
            // Use the Windows application icon below.
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    public static void Apply(Form form)
    {
        form.ShowIcon = true;
        form.Icon = Load();
    }
}
