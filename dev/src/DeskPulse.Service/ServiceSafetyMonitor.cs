using System.Diagnostics;

namespace DeskPulse;

internal sealed class ServiceSafetyMonitor : IDisposable
{
    private readonly object _sync = new();
    private readonly Action<string> _warning;
    private readonly Action<string> _critical;
    private readonly Func<bool> _isPaused;
    private CancellationTokenSource? _cts;
    private Task? _task;
    private string _state = "Normal";
    private string _message = "Service resources are within configured limits.";
    private double _cpu;
    private double _memory;

    public ServiceSafetyMonitor(Action<string> warning, Action<string> critical, Func<bool> isPaused)
    { _warning = warning; _critical = critical; _isPaused = isPaused; }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _task = Task.Run(() => RunAsync(_cts.Token));
    }

    public string GetStatus()
    {
        lock (_sync)
            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"OK|STATE={_state}|CPU={_cpu:0.0}|MEMORY={_memory:0.0}|MESSAGE={Escape(_message)}");
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var process = Process.GetCurrentProcess();
        var previousCpu = process.TotalProcessorTime;
        var previousStamp = Stopwatch.GetTimestamp();
        var warningSince = DateTimeOffset.MinValue;
        var criticalSince = DateTimeOffset.MinValue;
        var warningIssued = false;

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(1000, token);
            var settings = AppSettings.Load();
            process.Refresh();
            var nowCpu = process.TotalProcessorTime;
            var nowStamp = Stopwatch.GetTimestamp();
            var wall = (nowStamp - previousStamp) / (double)Stopwatch.Frequency;
            var cpu = wall > 0 ? (nowCpu - previousCpu).TotalSeconds / wall / Math.Max(1, Environment.ProcessorCount) * 100.0 : 0;
            previousCpu = nowCpu; previousStamp = nowStamp;
            var memory = GetMemoryPercent(process.WorkingSet64);

            lock (_sync) { _cpu = Math.Max(0, cpu); _memory = Math.Max(0, memory); }
            if (_isPaused()) continue;

            var critical = cpu >= settings.ServiceSafetyCriticalCpuPercent || memory >= settings.ServiceSafetyCriticalMemoryPercent;
            var warning = cpu >= settings.ServiceSafetyWarningCpuPercent || memory >= settings.ServiceSafetyWarningMemoryPercent;

            if (critical)
            {
                if (criticalSince == DateTimeOffset.MinValue) criticalSince = DateTimeOffset.UtcNow;
                if ((DateTimeOffset.UtcNow - criticalSince).TotalSeconds >= settings.ServiceSafetyCriticalSustainedSeconds)
                {
                    var restartBehaviour = settings.PauseLoggingAtStartupAfterSafetyTrigger
                        ? " The safety pause will remain active after restart until logging is resumed manually."
                        : " The safety pause applies to the current run; logging will resume after a service or Windows restart.";
                    var msg = $"Critical DeskPulse.Service resource use persisted: CPU {cpu:0.0}%, RAM {memory:0.0}%. Logging was placed in a safety pause.{restartBehaviour}";
                    lock (_sync) { _state = "CriticalPaused"; _message = msg; }
                    _critical(msg);
                    criticalSince = DateTimeOffset.MinValue;
                }
            }
            else criticalSince = DateTimeOffset.MinValue;

            if (warning)
            {
                if (warningSince == DateTimeOffset.MinValue) warningSince = DateTimeOffset.UtcNow;
                if (!warningIssued && (DateTimeOffset.UtcNow - warningSince).TotalSeconds >= settings.ServiceSafetyWarningSustainedSeconds)
                {
                    var msg = $"DeskPulse.Service resource warning: CPU {cpu:0.0}%, RAM {memory:0.0}%.";
                    lock (_sync) { _state = "Warning"; _message = msg; }
                    _warning(msg); warningIssued = true;
                }
            }
            else
            {
                warningSince = DateTimeOffset.MinValue; warningIssued = false;
                lock (_sync) { if (_state != "CriticalPaused") { _state = "Normal"; _message = "Service resources are within configured limits."; } }
            }
        }
    }

    public void ClearCriticalState()
    { lock (_sync) { _state = "Normal"; _message = "Persistent safety pause cleared by the user."; } }

    private static double GetMemoryPercent(long workingSet)
    {
        var info = GC.GetGCMemoryInfo();
        var total = info.TotalAvailableMemoryBytes;
        return total > 0 ? workingSet / (double)total * 100.0 : 0;
    }
    private static string Escape(string value) => value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
    public void Dispose() { _cts?.Cancel(); try { _task?.Wait(TimeSpan.FromSeconds(2)); } catch { } _cts?.Dispose(); }
}
