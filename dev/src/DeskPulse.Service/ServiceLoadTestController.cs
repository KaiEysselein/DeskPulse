using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskPulse;

internal sealed class ServiceLoadTestController : IDisposable
{
    private const double MaximumResourcePercent = 50.0;
    private const int MaximumDurationSeconds = 300;
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private Task? _activeTask;
    private Guid _activeTestId;
    private string _state = "Idle";
    private string _outcome = "No diagnostic load test has been run.";
    private double _cpuTarget;
    private double _memoryTarget;
    private int _durationSeconds;
    private DateTimeOffset? _startedUtc;
    private long _allocatedBytes;
    private double _currentCpuPercent;
    private long _currentWorkingSetBytes;
    private double _systemMemoryUsedPercent;

    public string Start(double cpuPercent, double memoryPercent, int durationSeconds, Action<string>? log)
    {
        if (cpuPercent < 0 || memoryPercent < 0)
            return "ERROR|CPU and memory percentages cannot be negative.";
        if (cpuPercent > MaximumResourcePercent || memoryPercent > MaximumResourcePercent)
            return $"ERROR|Diagnostic load tests are limited to {MaximumResourcePercent:0}% per resource.";
        if (cpuPercent <= 0 && memoryPercent <= 0)
            return "ERROR|Specify a CPU or memory target greater than zero.";
        if (durationSeconds < 1 || durationSeconds > MaximumDurationSeconds)
            return $"ERROR|Duration must be between 1 and {MaximumDurationSeconds} seconds.";

        lock (_sync)
        {
            if (_activeTask is { IsCompleted: false })
                return "ERROR|A diagnostic service load test is already running.";

            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            var testCancellation = _cancellation;
            var testId = Guid.NewGuid();
            _activeTestId = testId;
            _cpuTarget = cpuPercent;
            _memoryTarget = memoryPercent;
            _durationSeconds = durationSeconds;
            _startedUtc = DateTimeOffset.UtcNow;
            _allocatedBytes = 0;
            _currentCpuPercent = 0;
            _currentWorkingSetBytes = Process.GetCurrentProcess().WorkingSet64;
            _systemMemoryUsedPercent = GetSystemMemoryUsedPercent();
            _state = "Starting";
            _outcome = "Diagnostic load test is starting.";
            _activeTask = Task.Run(() => RunAsync(testId, cpuPercent, memoryPercent, durationSeconds, log, testCancellation, testCancellation.Token));
        }

        log?.Invoke($"Intentional diagnostic load test started. CPU target {cpuPercent:0.##}%, memory target {memoryPercent:0.##}%, duration {durationSeconds} seconds. Hard cap: 50% per resource.");
        return $"OK|STARTED|CPU={cpuPercent:0.##}|MEMORY={memoryPercent:0.##}|DURATION={durationSeconds}";
    }

    public string Stop(Action<string>? log)
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            if (_activeTask is not { IsCompleted: false })
                return "OK|NOT_RUNNING";
            cancellation = _cancellation;
            _state = "Stopping";
            _outcome = "Cancellation requested.";
        }

        cancellation?.Cancel();
        log?.Invoke("Intentional diagnostic load test cancellation requested.");
        return "OK|STOPPING";
    }

    public string GetStatus()
    {
        lock (_sync)
        {
            var elapsed = _startedUtc.HasValue
                ? Math.Max(0, (DateTimeOffset.UtcNow - _startedUtc.Value).TotalSeconds)
                : 0;
            if (_state is "Completed" or "Cancelled" or "Error" or "Idle")
                elapsed = Math.Min(elapsed, _durationSeconds);

            return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"OK|STATE={Escape(_state)}|CPU_TARGET={_cpuTarget:0.##}|MEMORY_TARGET={_memoryTarget:0.##}|DURATION={_durationSeconds}|ELAPSED={elapsed:0.0}|CPU_CURRENT={_currentCpuPercent:0.0}|WORKING_SET={_currentWorkingSetBytes}|ALLOCATED={_allocatedBytes}|SYSTEM_MEMORY_USED={_systemMemoryUsedPercent:0.0}|MESSAGE={Escape(_outcome)}");
        }
    }

    private async Task RunAsync(Guid testId, double cpuPercent, double memoryPercent, int durationSeconds, Action<string>? log, CancellationTokenSource testCancellation, CancellationToken token)
    {
        var workers = new List<Task>();
        var allocations = new List<byte[]>();
        var outcome = "completed normally";
        var finalState = "Completed";

        try
        {
            lock (_sync)
            {
                if (_activeTestId == testId)
                {
                    _state = "Running";
                    _outcome = "Diagnostic load test is running.";
                }
            }

            if (cpuPercent > 0)
            {
                var workerCount = Math.Max(1, Environment.ProcessorCount);
                for (var i = 0; i < workerCount; i++)
                    workers.Add(Task.Run(() => RunCpuWorkerAsync(cpuPercent, token), token));
            }

            if (memoryPercent > 0)
                await AllocateMemoryAsync(memoryPercent, allocations, bytes => UpdateAllocated(testId, bytes), token);

            await MonitorAsync(testId, durationSeconds, token);
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            finalState = "Cancelled";
        }
        catch (Exception ex)
        {
            outcome = "stopped because of an error: " + ex.Message;
            finalState = "Error";
        }
        finally
        {
            try { testCancellation.Cancel(); } catch { }
            try { await Task.WhenAll(workers); } catch (OperationCanceledException) { } catch { }
            allocations.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            lock (_sync)
            {
                if (_activeTestId == testId)
                {
                    _allocatedBytes = 0;
                    _currentWorkingSetBytes = Process.GetCurrentProcess().WorkingSet64;
                    _systemMemoryUsedPercent = GetSystemMemoryUsedPercent();
                    _state = finalState;
                    _outcome = "Last test " + outcome + ".";
                }
            }

            log?.Invoke("Intentional diagnostic load test " + outcome + ".");
        }
    }

    private async Task MonitorAsync(Guid testId, int durationSeconds, CancellationToken token)
    {
        using var process = Process.GetCurrentProcess();
        var previousCpu = process.TotalProcessorTime;
        var previousTime = Stopwatch.GetTimestamp();
        var end = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);

        while (DateTimeOffset.UtcNow < end)
        {
            await Task.Delay(500, token);
            process.Refresh();
            var nowCpu = process.TotalProcessorTime;
            var nowTime = Stopwatch.GetTimestamp();
            var wallSeconds = (nowTime - previousTime) / (double)Stopwatch.Frequency;
            var cpuSeconds = (nowCpu - previousCpu).TotalSeconds;
            var cpuPercent = wallSeconds > 0
                ? cpuSeconds / wallSeconds / Math.Max(1, Environment.ProcessorCount) * 100.0
                : 0;
            previousCpu = nowCpu;
            previousTime = nowTime;

            lock (_sync)
            {
                if (_activeTestId != testId)
                    return;
                _currentCpuPercent = Math.Max(0, cpuPercent);
                _currentWorkingSetBytes = process.WorkingSet64;
                _systemMemoryUsedPercent = GetSystemMemoryUsedPercent();
            }
        }
    }

    private void UpdateAllocated(Guid testId, long bytes)
    {
        lock (_sync)
        {
            if (_activeTestId == testId)
                _allocatedBytes = bytes;
        }
    }

    private static async Task RunCpuWorkerAsync(double targetPercent, CancellationToken token)
    {
        const int cycleMilliseconds = 100;
        var busyMilliseconds = Math.Clamp((int)Math.Round(cycleMilliseconds * targetPercent / 100.0), 1, 50);
        var idleMilliseconds = cycleMilliseconds - busyMilliseconds;
        var stopwatch = new Stopwatch();

        while (!token.IsCancellationRequested)
        {
            stopwatch.Restart();
            while (stopwatch.ElapsedMilliseconds < busyMilliseconds && !token.IsCancellationRequested)
                Thread.SpinWait(4000);

            if (idleMilliseconds > 0)
                await Task.Delay(idleMilliseconds, token);
        }
    }

    private static async Task AllocateMemoryAsync(double targetPercent, List<byte[]> allocations, Action<long> reportAllocated, CancellationToken token)
    {
        var totalPhysicalBytes = GetTotalPhysicalMemoryBytes();
        if (totalPhysicalBytes <= 0)
            throw new InvalidOperationException("Total physical memory could not be determined.");

        var targetWorkingSet = (long)(totalPhysicalBytes * (targetPercent / 100.0));
        var currentWorkingSet = Process.GetCurrentProcess().WorkingSet64;
        var bytesToAllocate = Math.Max(0, targetWorkingSet - currentWorkingSet);
        var allocated = 0L;
        const int chunkSize = 16 * 1024 * 1024;

        while (bytesToAllocate > 0)
        {
            token.ThrowIfCancellationRequested();
            var size = (int)Math.Min(chunkSize, bytesToAllocate);
            var block = GC.AllocateUninitializedArray<byte>(size, pinned: false);
            for (var offset = 0; offset < block.Length; offset += 4096)
                block[offset] = 1;
            allocations.Add(block);
            allocated += size;
            reportAllocated(allocated);
            bytesToAllocate -= size;
            await Task.Delay(10, token);
        }
    }

    private static string Escape(string value) => value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");

    private static double GetSystemMemoryUsedPercent()
    {
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status) ? status.MemoryLoad : 0;
    }

    private static ulong GetTotalPhysicalMemoryBytes()
    {
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status) ? status.TotalPhys : 0;
    }

    public void Dispose()
    {
        try { _cancellation?.Cancel(); } catch { }
        try { _activeTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cancellation?.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
}
