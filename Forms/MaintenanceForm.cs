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

public sealed partial class MaintenanceForm : SettingsForm
{
    public MaintenanceForm() : base(maintenanceOnly: true)
    {
    }
}
