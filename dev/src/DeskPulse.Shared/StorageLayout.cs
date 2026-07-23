using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.Win32.SafeHandles;

namespace DeskPulse;

public static class StorageLayout
{
    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DeskPulse");

    public static string SystemFolder => Path.Combine(RootFolder, "System");

    public static string SystemDatabaseFilePath => Path.Combine(SystemFolder, "DeskPulse-System.db");

    public static string UsersFolder => Path.Combine(RootFolder, "Users");

    public static string GetUserFolder(string windowsSid) =>
        Path.Combine(UsersFolder, ValidateSid(windowsSid));

    public static string GetUserDatabaseFilePath(string windowsSid) =>
        Path.Combine(GetUserFolder(windowsSid), "DeskPulse.db");

    public static bool TryGetUserSidFromDatabaseFilePath(string databaseFilePath, out string windowsSid)
    {
        windowsSid = string.Empty;
        try
        {
            var parent = Directory.GetParent(Path.GetFullPath(databaseFilePath));
            var usersRoot = Path.GetFullPath(UsersFolder).TrimEnd(Path.DirectorySeparatorChar);
            if (parent == null ||
                !string.Equals(
                    parent.Parent?.FullName?.TrimEnd(Path.DirectorySeparatorChar),
                    usersRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            windowsSid = new SecurityIdentifier(parent.Name).Value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string ResolveCurrentOrInteractiveUserSid()
    {
        if (TryResolveCurrentOrInteractiveUserSid(out var windowsSid))
            return windowsSid;

        throw new InvalidOperationException(
            "DeskPulse could not resolve the active interactive user's Windows SID.");
    }

    public static bool TryResolveCurrentOrInteractiveUserSid(out string windowsSid)
    {
        return TryResolveCurrentOrInteractiveUser(out windowsSid, out _, out _);
    }

    public static bool TryResolveCurrentOrInteractiveUser(
        out string windowsSid,
        out int sessionId,
        out string userName)
    {
        using var currentIdentity = WindowsIdentity.GetCurrent();
        if (currentIdentity.User != null &&
            !currentIdentity.User.IsWellKnown(WellKnownSidType.LocalSystemSid))
        {
            windowsSid = currentIdentity.User.Value;
            sessionId = Process.GetCurrentProcess().SessionId;
            userName = currentIdentity.Name;
            return true;
        }

        var consoleSessionId = NativeMethods.WTSGetActiveConsoleSessionId();
        if (consoleSessionId == uint.MaxValue ||
            !TryResolveSessionUser(checked((int)consoleSessionId), out windowsSid, out userName))
        {
            windowsSid = string.Empty;
            sessionId = -1;
            userName = string.Empty;
            return false;
        }

        sessionId = checked((int)consoleSessionId);
        return true;
    }

    public static bool TryResolveSessionUser(
        int sessionId,
        out string windowsSid,
        out string userName)
    {
        windowsSid = string.Empty;
        userName = string.Empty;
        if (sessionId < 0 ||
            !NativeMethods.WTSQueryUserToken(checked((uint)sessionId), out var userToken))
        {
            return false;
        }

        using (userToken)
        using (var interactiveIdentity = new WindowsIdentity(userToken.DangerousGetHandle()))
        {
            windowsSid = interactiveIdentity.User?.Value ?? string.Empty;
            userName = interactiveIdentity.Name;
            return windowsSid.Length > 0;
        }
    }

    public static void PrepareSystemStorage()
    {
        Directory.CreateDirectory(RootFolder);
        var systemFolder = Directory.CreateDirectory(SystemFolder);
        SetStorageAcl(
            systemFolder,
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
    }

    public static void PrepareUserStorage(string windowsSid)
    {
        var userSid = new SecurityIdentifier(ValidateSid(windowsSid));
        Directory.CreateDirectory(RootFolder);
        Directory.CreateDirectory(UsersFolder);
        var userFolder = Directory.CreateDirectory(GetUserFolder(userSid.Value));
        SetStorageAcl(userFolder, userSid);
    }

    private static void SetStorageAcl(DirectoryInfo folder, SecurityIdentifier readOnlySid)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            readOnlySid,
            FileSystemRights.ReadAndExecute | FileSystemRights.ReadAttributes |
            FileSystemRights.ReadExtendedAttributes | FileSystemRights.Synchronize,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        folder.SetAccessControl(security);
    }

    private static string ValidateSid(string windowsSid)
    {
        if (string.IsNullOrWhiteSpace(windowsSid))
            throw new ArgumentException("A Windows SID is required.", nameof(windowsSid));

        var sid = new SecurityIdentifier(windowsSid.Trim());
        return sid.Value;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        internal static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WTSQueryUserToken(
            uint sessionId,
            out SafeAccessTokenHandle token);
    }
}

public static class StorageMigration
{
    public static void EnsureUserDatabase(string legacyDatabaseFilePath, string windowsSid)
    {
        var targetDatabaseFilePath = StorageLayout.GetUserDatabaseFilePath(windowsSid);
        StorageLayout.PrepareUserStorage(windowsSid);

        if (File.Exists(targetDatabaseFilePath) ||
            !File.Exists(legacyDatabaseFilePath) ||
            PathsEqual(legacyDatabaseFilePath, targetDatabaseFilePath))
        {
            return;
        }

        var temporaryDatabaseFilePath = targetDatabaseFilePath + ".migrating";
        var backupDatabaseFilePath = legacyDatabaseFilePath + ".pre-programdata-migration.bak";

        DeleteDatabaseFiles(temporaryDatabaseFilePath);
        try
        {
            BackupDatabase(legacyDatabaseFilePath, temporaryDatabaseFilePath);
            ValidateDatabase(temporaryDatabaseFilePath);

            if (!File.Exists(backupDatabaseFilePath))
                BackupDatabase(legacyDatabaseFilePath, backupDatabaseFilePath);

            File.Move(temporaryDatabaseFilePath, targetDatabaseFilePath);
        }
        catch
        {
            DeleteDatabaseFiles(temporaryDatabaseFilePath);
            throw;
        }
    }

    private static void BackupDatabase(string sourcePath, string destinationPath)
    {
        var destinationFolder = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationFolder))
            Directory.CreateDirectory(destinationFolder);

        using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static void ValidateDatabase(string databaseFilePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = command.ExecuteScalar()?.ToString();
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"SQLite integrity validation failed after migration: {result ?? "no result"}");
    }

    private static void DeleteDatabaseFiles(string databaseFilePath)
    {
        foreach (var path in new[] { databaseFilePath, databaseFilePath + "-wal", databaseFilePath + "-shm" })
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // The original exception is more useful if cleanup also fails.
            }
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
