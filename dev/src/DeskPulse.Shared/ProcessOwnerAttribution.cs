using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DeskPulse;

internal static class ProcessOwnerAttribution
{
    private const uint TokenQuery = 0x0008;
    private const int MaximumCacheEntries = 4096;
    private static readonly ConcurrentDictionary<int, CacheEntry> Cache = new();

    public static EventAttribution Resolve(int processId)
    {
        if (processId <= 0 || processId == 4)
            return EventAttribution.System;

        try
        {
            using var process = Process.GetProcessById(processId);
            var startedUtcTicks = SafeStartTimeUtcTicks(process);
            if (startedUtcTicks != 0 &&
                Cache.TryGetValue(processId, out var cached) &&
                cached.StartedUtcTicks == startedUtcTicks)
                return cached.Attribution;

            var attribution = ResolveTokenOwner(process) ?? ResolveSessionOwner(process);
            if (Cache.Count >= MaximumCacheEntries)
                RemoveExpiredEntries();
            if (startedUtcTicks != 0)
                Cache[processId] = new CacheEntry(startedUtcTicks, attribution);
            return attribution;
        }
        catch
        {
            return EventAttribution.System;
        }
    }

    internal static bool IsSystemIdentity(string sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
            return false;
        try
        {
            var identity = new SecurityIdentifier(sid);
            return identity.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                   identity.IsWellKnown(WellKnownSidType.LocalServiceSid) ||
                   identity.IsWellKnown(WellKnownSidType.NetworkServiceSid) ||
                   sid.StartsWith("S-1-5-80-", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static EventAttribution? ResolveTokenOwner(Process process)
    {
        if (!OpenProcessToken(process.Handle, TokenQuery, out var token))
            return null;
        try
        {
            using var identity = new WindowsIdentity(token);
            var sid = identity.User?.Value ?? "";
            if (sid.Length == 0)
                return null;
            if (identity.IsSystem || IsSystemIdentity(sid))
                return EventAttribution.System;
            return EventAttribution.User(
                sid,
                process.SessionId,
                string.IsNullOrWhiteSpace(identity.Name) ? sid : identity.Name);
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static EventAttribution ResolveSessionOwner(Process process)
    {
        return StorageLayout.TryResolveSessionUser(
            process.SessionId,
            out var windowsSid,
            out var userName)
            ? EventAttribution.User(windowsSid, process.SessionId, userName)
            : EventAttribution.System;
    }

    private static long SafeStartTimeUtcTicks(Process process)
    {
        try { return process.StartTime.ToUniversalTime().Ticks; }
        catch { return 0; }
    }

    private static void RemoveExpiredEntries()
    {
        foreach (var item in Cache.Take(Math.Max(1, Cache.Count / 4)))
            Cache.TryRemove(item.Key, out _);
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private sealed record CacheEntry(long StartedUtcTicks, EventAttribution Attribution);
}
