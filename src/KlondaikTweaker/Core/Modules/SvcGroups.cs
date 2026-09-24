namespace KlondaikTweaker.Core.Modules;

public static class SvcGroups
{
    private static readonly (string Group, string[] Names)[] Table =
    [
        ("print", ["Spooler", "PrintNotify", "PrintDeviceConfigurationService", "PrintScanBrokerService", "StiSvc", "Fax", "McpManagementService"]),
        ("bluetooth", ["bthserv", "BthAvctpSvc", "BTAGService", "BthHFSrv"]),
        ("vpn", ["RasMan", "RasAuto", "SstpSvc", "IKEEXT", "PolicyAgent", "RemoteAccess", "WinHttpAutoProxySvc"]),
        ("lan", ["LanmanServer", "LanmanWorkstation", "Browser", "SSDPSRV", "upnphost", "FDResPub", "fdPHost", "lltdsvc", "NetTcpPortSharing", "WFDSConMgrSvc", "HomeGroupListener", "HomeGroupProvider"]),
        ("remote", ["TermService", "UmRdpService", "SessionEnv", "RemoteRegistry", "WinRM", "Wecsvc"]),
        ("diag", ["DPS", "WdiServiceHost", "WdiSystemHost", "diagsvc", "WerSvc", "wercplsupport", "diagnosticshub.standardcollector.service"]),
        ("telemetry", ["DiagTrack", "dmwappushservice"]),
        ("hyperv", ["HvHost", "vmcompute", "vmms"]),
        ("media", ["WMPNetworkSvc"]),
        ("vr", ["SharedRealitySvc", "SpatialGraphFilter"]),
        ("disk", ["defragsvc", "smphost", "vds", "MSiSCSI", "TrkWks", "SysMain"]),
        ("backup", ["VSS", "swprv", "wbengine", "SDRSVC"]),
        ("index", ["WSearch"]),
        ("data", ["DusmSvc"]),
        ("compat", ["PcaSvc"]),
        ("autoplay", ["ShellHWDetection"]),
        ("devices", ["DeviceInstall", "DsmSvc", "DeviceAssociationService", "DevQueryBroker"]),
        ("xbox", ["XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc", "GamingServices", "GamingServicesNet", "xbgm"]),
        ("update", ["wuauserv", "UsoSvc", "WaaSMedicSvc", "BITS", "DoSvc", "InstallService", "ClipSVC", "AppXSvc", "LicenseManager"]),
        ("sensors", ["SensorService", "SensrSvc", "SensorDataService", "lfsvc"]),
        ("cards", ["SEMgrSvc", "WalletService", "WbioSrvc", "SCardSvr", "ScDeviceEnum", "SCPolicySvc"]),
        ("phone", ["PhoneSvc", "TapiSrv", "icssvc", "WwanSvc"]),
        ("wifi", ["WlanSvc", "dot3svc", "EapHost"]),
        ("input", ["TextInputManagementService", "TabletInputService", "TouchKeyboard"])
    ];

    private static readonly (string Prefix, string Group)[] Prefixes =
    [
        ("PrintWorkflowUserSvc", "print"),
        ("BluetoothUserService", "bluetooth"),
        ("vmic", "hyperv"),
        ("MixedReality", "vr"),
        ("CloudBackupRestoreSvc", "backup"),
        ("PimIndexMaintenanceSvc", "index"),
        ("DevicesFlowUserSvc", "devices"),
        ("DevicePickerUserSvc", "devices"),
        ("MessagingService", "phone")
    ];

    private static readonly Dictionary<string, string> Exact = Build();

    private static Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (group, names) in Table)
            foreach (var name in names)
                map[name] = group;
        return map;
    }

    public static string Of(string name)
    {
        if (name.Length == 0) return "other";
        if (Exact.TryGetValue(name, out var group)) return group;

        var cut = Strip(name);
        if (cut.Length != name.Length && Exact.TryGetValue(cut, out group)) return group;

        foreach (var (prefix, g) in Prefixes)
            if (cut.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return g;

        return "other";
    }

    private static string Strip(string name)
    {
        var at = name.LastIndexOf('_');
        if (at <= 0 || at == name.Length - 1) return name;
        for (var i = at + 1; i < name.Length; i++)
            if (!Uri.IsHexDigit(name[i])) return name;
        return name[..at];
    }

    public static List<string> Ids() => Table.Select(x => x.Group).Concat(Prefixes.Select(x => x.Group)).Distinct(StringComparer.Ordinal).ToList();
}
