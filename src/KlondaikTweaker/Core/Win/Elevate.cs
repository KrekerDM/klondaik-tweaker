using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace KlondaikTweaker.Core.Win;

public static class Elevate
{
    private const int SE_PRIVILEGE_ENABLED = 0x0002;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint Low;
        public int High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public int Count;
        public Luid Luid;
        public int Attributes;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValueW(string? host, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges state, int length, IntPtr previous, IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    private static readonly string[] Wanted =
    [
        "SeTakeOwnershipPrivilege",
        "SeRestorePrivilege",
        "SeBackupPrivilege",
        "SeDebugPrivilege",
        "SeImpersonatePrivilege",
        "SeSecurityPrivilege",
        "SeIncreaseQuotaPrivilege",
        "SeProfileSingleProcessPrivilege",
        "SeShutdownPrivilege",
        "SeAssignPrimaryTokenPrivilege"
    ];

    private static bool _done;
    private static readonly object Gate = new();

    public static List<string> Granted { get; } = [];

    public static void EnableAll()
    {
        lock (Gate)
        {
            if (_done) return;
            _done = true;

            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token)) return;
            try
            {
                foreach (var name in Wanted)
                {
                    if (!LookupPrivilegeValueW(null, name, out var luid)) continue;
                    var state = new TokenPrivileges { Count = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
                    if (AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero) &&
                        Marshal.GetLastWin32Error() == 0)
                        Granted.Add(name);
                }
            }
            finally { CloseHandle(token); }
        }
    }

    private static SecurityIdentifier Admins => new(WellKnownSidType.BuiltinAdministratorsSid, null);

    public static bool UnlockKey(string hive, string path)
    {
        EnableAll();
        try
        {
            using var root = Reg.Root(hive);
            if (root is null) return false;

            using (var owner = root.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership))
            {
                if (owner is null) return false;
                var security = owner.GetAccessControl(AccessControlSections.None);
                security.SetOwner(Admins);
                owner.SetAccessControl(security);
            }

            using (var acl = root.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions | RegistryRights.ReadPermissions))
            {
                if (acl is null) return false;
                var security = acl.GetAccessControl(AccessControlSections.Access);
                security.AddAccessRule(new RegistryAccessRule(
                    Admins,
                    RegistryRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                acl.SetAccessControl(security);
            }

            return true;
        }
        catch { return false; }
    }

    public static bool UnlockTree(string hive, string path)
    {
        var ok = UnlockKey(hive, path);
        foreach (var child in Reg.SubKeys(hive, path))
        {
            UnlockTree(hive, path + "\\" + child);
        }
        return ok;
    }
}
