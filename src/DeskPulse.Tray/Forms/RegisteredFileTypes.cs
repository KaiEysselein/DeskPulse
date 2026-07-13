using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;

namespace DeskPulse;

public sealed class RegisteredFileTypeInfo
{
    public string Extension { get; set; } = "";
    public string Description { get; set; } = "";

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Description))
                return Extension;

            return $"{Extension} - {Description}";
        }
    }

    public override string ToString()
    {
        return Extension;
    }
}

public static class RegisteredFileTypeReader
{
    public static List<RegisteredFileTypeInfo> ReadRegisteredFileTypes()
    {
        var result = new List<RegisteredFileTypeInfo>();

        try
        {
            foreach (var subKeyName in Registry.ClassesRoot.GetSubKeyNames())
            {
                if (!subKeyName.StartsWith(".", StringComparison.Ordinal))
                    continue;

                if (subKeyName.Length < 2)
                    continue;

                var description = "";

                try
                {
                    using var extensionKey = Registry.ClassesRoot.OpenSubKey(subKeyName);
                    var className = extensionKey?.GetValue(null)?.ToString() ?? "";

                    if (!string.IsNullOrWhiteSpace(className))
                    {
                        using var classKey = Registry.ClassesRoot.OpenSubKey(className);
                        description = classKey?.GetValue(null)?.ToString() ?? "";
                    }
                }
                catch
                {
                    description = "";
                }

                result.Add(new RegisteredFileTypeInfo
                {
                    Extension = subKeyName.ToLowerInvariant(),
                    Description = description
                });
            }
        }
        catch
        {
            // Return whatever could be read.
        }

        return result
            .GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
