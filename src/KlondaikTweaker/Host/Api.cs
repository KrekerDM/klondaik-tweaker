using System.Text.Json;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Modules;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Host;

public static class Api
{
    public delegate void Push(string channel, object data);

    public static event Action? QuitRequested;
    public static Func<string[]>? FilesRequested;

    private static string Lang => Settings.Data.Lang;

    private static readonly HashSet<string> LatinText = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "de", "pl", "es", "fr"
    };

    private static bool PrefersEnglish => LatinText.Contains(Lang);

    private static string TextLang => PrefersEnglish ? "en" : "ru";

    private static string S(JsonElement? p, string name, string fallback = "")
    {
        if (p is null || p.Value.ValueKind != JsonValueKind.Object) return fallback;
        return p.Value.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    }

    private static bool B(JsonElement? p, string name, bool fallback = false)
    {
        if (p is null || p.Value.ValueKind != JsonValueKind.Object) return fallback;
        if (!p.Value.TryGetProperty(name, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    private static int[] Nums(JsonElement? p, string name)
    {
        if (p is null || p.Value.ValueKind != JsonValueKind.Object) return [];
        if (!p.Value.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) return [];
        return v.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.Number)
            .Select(x => x.TryGetInt32(out var n) ? n : -1)
            .Where(x => x >= 0)
            .ToArray();
    }

    private static string[] Arr(JsonElement? p, string name)
    {
        if (p is null || p.Value.ValueKind != JsonValueKind.Object) return [];
        if (!p.Value.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) return [];
        return v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").ToArray();
    }

    public static object? Handle(string method, JsonElement? p, Push push)
    {
        switch (method)
        {
            case "app.info": return AppInfo();
            case "app.settings": return Settings.Data;
            case "update.auto": return UpdateAuto();
            case "app.setSetting": return SetSetting(p);
            case "app.strings": return new { lang = Lang };
            case "app.quit": QuitRequested?.Invoke(); return new { ok = true };

            case "tweaks.list": return TweakList(p);
            case "tweaks.detect": return Detect(Arr(p, "ids"));
            case "tweaks.apply": return ApplyMany(Arr(p, "ids"), true, push);
            case "tweaks.revert": return ApplyMany(Arr(p, "ids"), false, push);
            case "tweaks.presets": return Presets();

            case "wizard.questions": return WizardQuestions();
            case "wizard.resolve": return WizardResolve(p);

            case "journal.list": return JournalList();
            case "journal.revert": return JournalRevert(S(p, "id"));
            case "journal.revertAll": return JournalRevertAll(push);
            case "journal.clear": Journal.Clear(); return new { ok = true };

            case "restore.status": return new { enabled = RestorePoint.Enabled(), points = RestorePoint.List().Take(12).Select(x => new { seq = x.Seq, desc = x.Desc, time = x.Time }).ToList() };
            case "restore.enable": return new { result = RestorePoint.Enable() };
            case "restore.create": { var r = RestorePoint.Create(S(p, "desc", "Klondaik Tweaker")); return new { ok = r.Ok, message = r.Message }; }
            case "restore.open": RestorePoint.OpenUi(); return new { ok = true };

            case "clean.scan": return new { targets = Cleaner.Scan(), disks = Cleaner.DiskInfo() };
            case "clean.run": return CleanRun(Arr(p, "ids"), push);
            case "clean.deep": return new { result = Cleaner.DeepComponentCleanup(B(p, "resetBase")) };
            case "clean.eventlogs": return new { result = Cleaner.ClearEventLogs() };

            case "startup.list": return Startup.List();
            case "startup.toggle": return new { ok = Startup.SetEnabled(S(p, "id"), B(p, "enabled")) };
            case "startup.delete": return new { ok = Startup.Delete(S(p, "id")) };
            case "startup.add": return AddStartup(S(p, "path"));
            case "startup.pick":
            {
                var picked = FilesRequested?.Invoke() ?? [];
                if (picked.Length == 0) return new { ok = false, cancelled = true, message = "" };
                return AddStartup(picked[0]);
            }

            case "services.list": return ServiceList();
            case "services.set": return ServiceSet(p);
            case "services.control": return ServiceControl(p);

            case "appx.list": return Appx.List(B(p, "refresh")).Select(x => new { x.Name, x.Display, x.Group, x.Framework, x.System });
            case "appx.remove": return AppxRemove(Arr(p, "names"), push);

            case "net.adapters": return new { adapters = NetworkTools.Adapters(), telemetry = NetworkTools.TelemetryBlocked(), hosts = HostsFile.Current().Count };
            case "net.tcp": return NetworkTools.TcpState();
            case "net.dns": return NetworkTools.DnsPresets();
            case "net.testDns": return NetworkTools.TestDns();
            case "net.setDns": return new { result = NetworkTools.SetDns(S(p, "adapter"), S(p, "primary"), S(p, "secondary")) };
            case "net.nagle": NetworkTools.SetNagle(B(p, "disabled", true)); return new { ok = true };
            case "net.reset": return new { result = NetworkTools.ResetStack() };
            case "net.ping": return NetworkTools.PingTargets();
            case "net.telemetry": return NetTelemetry(B(p, "block", true));

            case "bench.run": return BenchRun(S(p, "label", "run"), push);
            case "bench.history": return Benchmark.History();
            case "bench.clear": Benchmark.ClearHistory(); return new { ok = true };

            case "monitor.read": return HwMonitor.Read();
            case "monitor.top": return HwMonitor.TopProcesses(14);
            case "monitor.trim": { var t = HwMonitor.TrimMemory(); return new { processes = t.Trimmed, freed = t.Freed }; }

            case "boost.state": return GameBoost.State;
            case "boost.start": return GameBoost.Start();
            case "boost.stop": return GameBoost.Stop();

            case "soft.list": return new { winget = SoftCatalog.WingetKnown ?? true, items = SoftCatalog.Quick(), ready = SoftCatalog.Peek() is not null };
            case "soft.state":
            {
                var fresh = B(p, "refresh");
                return new { winget = SoftCatalog.HasWinget(fresh), items = SoftCatalog.List(fresh), ready = true };
            }
            case "soft.install": return new { result = SoftCatalog.Install(S(p, "id")) };
            case "soft.uninstall": return new { result = SoftCatalog.Uninstall(S(p, "id")) };
            case "soft.upgradeAll": return new { result = SoftCatalog.UpgradeAll() };

            case "repair.run": return RepairRun(S(p, "id"));
            case "ghosts.list": return Ghosts.List();
            case "ghosts.remove": return Ghosts.Remove(Arr(p, "ids"));
            case "nvidia.state": return Nvidia.State();
            case "nvidia.export": return Nvidia.Export();
            case "nvidia.import": return Nvidia.Import(S(p, "file"));
            case "nvidia.open": return Nvidia.Open();
            case "nvidia.folder": Sh.OpenExternal(Nvidia.Folder); return new { ok = true };
            case "power.state": return new { schemes = PowerPlans.Schemes(), files = PowerPlans.Files(), hidden = PowerPlans.HiddenCount() };
            case "power.activate": return PowerPlans.Activate(S(p, "guid"));
            case "power.delete": return PowerPlans.Delete(S(p, "guid"));
            case "power.export": return PowerPlans.Export(S(p, "guid"));
            case "power.import": return PowerPlans.Import(S(p, "file"));
            case "power.unhide": return PowerPlans.Unhide();
            case "power.folder": Sh.OpenExternal(PowerPlans.Folder); return new { ok = true };
            case "nic.adapters": return Nic.Adapters();
            case "nic.params": return Nic.Params(S(p, "id"));
            case "nic.set": return Nic.Set(S(p, "id"), S(p, "name"), S(p, "value"));
            case "nic.preset": return Nic.Preset(S(p, "id"), S(p, "preset"));
            case "nic.restart": return Nic.Restart(S(p, "id"));
            case "irq.list": return new { threads = Cpu.Threads(), hybrid = Cpu.Hybrid(), devices = Irq.Devices() };
            case "irq.bind": return Irq.Bind(S(p, "id"), Nums(p, "threads"), B(p, "priority"));
            case "irq.reset": return Irq.Reset(S(p, "id"));
            case "tasks.list": return TaskGroups.List(TextLang);
            case "tasks.setGroup": return TaskGroups.SetGroup(S(p, "id"), B(p, "enable"), TextLang);
            case "tasks.setOne": return TaskGroups.SetOne(S(p, "path"), B(p, "enable"));
            case "features.list": return Features.List(TextLang, B(p, "refresh"));
            case "features.set": return Features.Set(S(p, "name"), B(p, "enable"));
            case "repair.status": return new { ctxti = ShellMenu.TiInstalled, ctxown = ShellMenu.OwnInstalled };
            case "app.credits": return Credits();

            case "update.check": return Updater.Check();
            case "update.install": return UpdateInstall(S(p, "url"), push);

            case "sys.open": Sh.Run(S(p, "target"), S(p, "args"), 5000); return new { ok = true };
            case "sys.link": Sh.OpenExternal(S(p, "url")); return new { ok = true };
            case "sys.folder": Sh.OpenExternal(Paths.Root); return new { ok = true };
            case "sys.power": return Power(S(p, "action"));
            case "sys.refresh": Env.Invalidate(); Appx.Invalidate(); return AppInfo();

            default: throw new InvalidOperationException("unknown method: " + method);
        }
    }

    private static object AppInfo()
    {
        var f = Env.Facts;
        return new
        {
            version = typeof(Api).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
            settings = Settings.Data,
            facts = f,
            restore = RestorePoint.Enabled(),
            tweakCount = Catalog.Db.Tweaks.Count,
            journal = Journal.Entries.Count(x => !x.Reverted),
            boost = GameBoost.State.Active,
            dataDir = Paths.Root,
            releasesPage = Updater.ReleasesPage
        };
    }

    private static object UpdateAuto()
    {
        if (!Settings.Data.AutoUpdateCheck) return new { skipped = "off" };

        var last = Settings.Data.LastUpdateCheck;
        if (DateTime.TryParse(last, out var when) && DateTime.UtcNow - when < TimeSpan.FromHours(20))
            return new { skipped = "recent" };

        Settings.Update(s => s.LastUpdateCheck = DateTime.UtcNow.ToString("o"));
        var info = Updater.Check();
        return new { info.Available, info.Latest, info.Current, info.Size, info.Error };
    }

    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ru", "uk", "be", "kk", "uz", "az", "en", "de", "pl", "es", "fr"
    };

    private static object SetSetting(JsonElement? p)
    {
        var key = S(p, "key");
        Settings.Update(s =>
        {
            switch (key)
            {
                case "lang": s.Lang = Languages.Contains(S(p, "value", "ru")) ? S(p, "value", "ru") : "ru"; break;
                case "showExtreme": s.ShowExtreme = B(p, "value"); break;
                case "autoRestorePoint": s.AutoRestorePoint = B(p, "value"); break;
                case "monitor3d": s.Monitor3d = B(p, "value"); break;
                case "liveMonitor": s.LiveMonitor = B(p, "value"); break;
                case "reduced": s.Reduced = B(p, "value"); break;
                case "wizardDone": s.WizardDone = B(p, "value"); break;
                case "acceptedRisk": s.AcceptedRisk = B(p, "value"); break;
                case "autoUpdateCheck": s.AutoUpdateCheck = B(p, "value"); break;
                case "favorite":
                    {
                        var id = S(p, "value");
                        if (s.Favorites.Contains(id)) s.Favorites.Remove(id);
                        else s.Favorites.Add(id);
                        break;
                    }
            }
        });
        return Settings.Data;
    }

    private static object TweakList(JsonElement? p)
    {
        var lang = TextLang;
        var cat = S(p, "cat");
        var risk = S(p, "risk");
        var query = S(p, "q").Trim();

        var items = new List<TweakView>();
        foreach (var t in Catalog.Db.Tweaks)
        {
            if (cat.Length > 0 && cat != "all" && t.Cat != cat) continue;
            if (risk.Length > 0 && risk != "all" && t.Risk != risk) continue;
            var v = TweakEngine.ToView(t, lang);
            if (query.Length > 0 &&
                !v.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) &&
                !v.Desc.Contains(query, StringComparison.CurrentCultureIgnoreCase) &&
                !v.Id.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            items.Add(v);
        }

        var cats = Catalog.Db.Tweaks.GroupBy(x => x.Cat).ToDictionary(g => g.Key, g => g.Count());
        var risks = Catalog.Db.Tweaks.GroupBy(x => x.Risk).ToDictionary(g => g.Key, g => g.Count());
        return new { items, cats, risks, total = Catalog.Db.Tweaks.Count };
    }

    private static object Detect(string[] ids)
    {
        var res = new Dictionary<string, string>();
        foreach (var id in ids)
        {
            var t = Catalog.Find(id);
            if (t is null) continue;
            res[id] = TweakEngine.Detect(t).ToString().ToLowerInvariant();
        }
        return res;
    }

    private static object ApplyMany(string[] ids, bool apply, Push push)
    {
        var defs = ids.Select(Catalog.Find).Where(x => x is not null).Cast<TweakDef>().ToList();
        var results = new List<object>();
        bool restart = false;
        string? restorePoint = null;

        if (apply && Settings.Data.AutoRestorePoint && defs.Any(x => x.Risk != "safe"))
        {
            push("progress", new { stage = "restore", title = PrefersEnglish ? "Creating a restore point" : "Создаю точку восстановления", percent = 0, total = defs.Count, done = 0, phase = "start" });
            var rp = RestorePoint.Create("Klondaik Tweaker: " + DateTime.Now.ToString("dd.MM HH:mm"));
            restorePoint = rp.Ok ? "ok" : rp.Message;
        }

        int done = 0;
        foreach (var t in defs)
        {
            var title = PrefersEnglish ? t.En.T : t.Ru.T;
            push("progress", new { stage = t.Id, title, percent = done * 100 / Math.Max(1, defs.Count), total = defs.Count, done, phase = "start" });

            var r = apply ? TweakEngine.Apply(t) : TweakEngine.Revert(t);
            done++;

            var state = TweakEngine.Detect(t);
            var ok = r.Ok;
            var error = r.Error;

            if (ok && state == (apply ? TweakState.NotApplied : TweakState.Applied))
            {
                ok = false;
                error = PrefersEnglish
                    ? "the change did not stick: the system still reports the old value"
                    : "изменение не удержалось: система по-прежнему показывает прежнее значение";
            }

            push("progress", new { stage = t.Id, title, percent = done * 100 / Math.Max(1, defs.Count), total = defs.Count, done, phase = "done", ok, error });
            restart |= r.NeedsRestart;
            results.Add(new { id = t.Id, ok, error, state = state.ToString().ToLowerInvariant() });
        }

        return new { results, restart, restorePoint, applied = results.Count };
    }

    private static readonly HashSet<string> WeakBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "perf.mem-compression-off",
        "perf.sysmain-off",
        "perf.prefetch-off",
        "perf.paging-executive",
        "perf.paging-combining-off",
        "perf.hibernate-off"
    };

    private static object Presets()
    {
        var lang = TextLang;
        var groups = new (string Id, string Ru, string En, string DescRu, string DescEn, Func<TweakDef, bool> Match)[]
        {
            ("balanced", "Сбалансированный", "Balanced", "Только безопасные твики: телеметрия, мусор в интерфейсе, отзывчивость", "Safe tweaks only: telemetry, interface clutter, responsiveness", t => t.Risk == "safe"),
            ("gaming", "Игровой", "Gaming", "Всё безопасное плюс твики под FPS, задержки ввода и сеть", "Everything safe plus FPS, input latency and network tweaks", t => t.Risk != "extreme" && (t.Tags.Contains("fps") || t.Tags.Contains("latency") || t.Tags.Contains("gpu") || t.Risk == "safe")),
            ("privacy", "Приватность", "Privacy", "Телеметрия, реклама, Copilot, Recall, сбор данных", "Telemetry, ads, Copilot, Recall, data collection", t => t.Cat == "privacy" && t.Risk != "extreme"),
            ("weak", "Слабый ПК", "Weak PC", "Для старого железа: оформление, эскизы, фоновые службы и сборщики данных", "For old hardware: visual effects, thumbnails, background services and data collectors", t => t.Risk != "extreme" && !WeakBlocklist.Contains(t.Id) && (t.Cat == "weak" || t.Cat == "debloat" || t.Cat == "privacy" || t.Tags.Contains("ram") || t.Tags.Contains("cpu") || t.Tags.Contains("boot"))),
            ("max", "Максимум", "Maximum", "Всё, включая агрессивные твики. Только для опытных", "Everything including aggressive tweaks. Experts only", t => true)
        };

        return groups.Select(g =>
        {
            var ids = Catalog.Db.Tweaks.Where(t => Env.Meets(t.Req) && g.Match(t)).Select(t => t.Id).ToArray();
            return new
            {
                id = g.Id,
                title = PrefersEnglish ? g.En : g.Ru,
                desc = PrefersEnglish ? g.DescEn : g.DescRu,
                count = ids.Length,
                ids
            };
        });
    }

    private static object WizardQuestions()
    {
        var lang = TextLang;
        return Wizard.Questions().Select(q => new
        {
            id = q.Id,
            multi = q.Multi,
            title = PrefersEnglish ? q.En : q.Ru,
            sub = PrefersEnglish ? q.SubEn : q.SubRu,
            when = q.When,
            options = q.Options.Select(o => new
            {
                id = o.Id,
                title = PrefersEnglish ? o.En : o.Ru,
                hint = PrefersEnglish ? o.HintEn : o.HintRu
            })
        });
    }

    private static object WizardResolve(JsonElement? p)
    {
        var answers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (p is not null && p.Value.TryGetProperty("answers", out var a) && a.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in a.EnumerateObject())
            {
                var vals = new List<string>();
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    vals.AddRange(prop.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? ""));
                else if (prop.Value.ValueKind == JsonValueKind.String)
                    vals.Add(prop.Value.GetString() ?? "");
                answers[prop.Name] = vals;
            }
        }
        var ids = Wizard.Resolve(answers);
        var lang = TextLang;
        var items = ids.Select(id => Catalog.Find(id)).Where(x => x is not null).Cast<TweakDef>()
            .Select(t => TweakEngine.ToView(t, lang)).ToList();
        return new
        {
            items,
            counts = new
            {
                safe = items.Count(x => x.Risk == "safe"),
                advanced = items.Count(x => x.Risk == "advanced"),
                extreme = items.Count(x => x.Risk == "extreme"),
                already = items.Count(x => x.State == "applied")
            }
        };
    }

    private static object JournalList()
    {
        var lang = TextLang;
        return Journal.Entries.OrderByDescending(x => x.Utc).Take(400).Select(e =>
        {
            var def = Catalog.Find(e.TweakId);
            return new
            {
                id = e.Id,
                tweakId = e.TweakId,
                title = def is null ? e.Title : (PrefersEnglish ? def.En.T : def.Ru.T),
                group = e.Group,
                time = e.Utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                reverted = e.Reverted,
                items = e.Items.Count,
                risk = def?.Risk ?? "safe"
            };
        });
    }

    private static object JournalRevert(string id)
    {
        var e = Journal.ById(id);
        if (e is null) return new { ok = false, error = "not found" };
        if (e.Reverted) return new { ok = true };
        var errors = new List<string>();
        for (int i = e.Items.Count - 1; i >= 0; i--)
        {
            try { TweakEngine.RevertItem(e.Items[i]); }
            catch (Exception ex) { errors.Add(ex.Message); }
        }
        Journal.MarkReverted(e.Id);
        return new { ok = errors.Count == 0, error = errors.Count > 0 ? string.Join("; ", errors.Take(2)) : null };
    }

    private static object JournalRevertAll(Push push)
    {
        var entries = Journal.Entries.Where(x => !x.Reverted).OrderByDescending(x => x.Utc).ToList();
        int done = 0;
        var failed = 0;
        foreach (var e in entries)
        {
            for (int i = e.Items.Count - 1; i >= 0; i--)
            {
                try { TweakEngine.RevertItem(e.Items[i]); }
                catch { failed++; }
            }
            Journal.MarkReverted(e.Id);
            done++;
            push("progress", new { stage = e.TweakId, percent = done * 100 / Math.Max(1, entries.Count), total = entries.Count, done });
        }
        return new { reverted = done, failed };
    }

    private static object CleanRun(string[] ids, Push push)
    {
        push("progress", new { stage = "clean", percent = 10, total = ids.Length, done = 0 });
        var r = Cleaner.Clean(ids);
        push("progress", new { stage = "clean", percent = 100, total = ids.Length, done = ids.Length });
        return new { freed = r.Freed, files = r.Files, errors = r.Errors };
    }

    private static readonly string[] SafeToDisable =
    [
        "DiagTrack", "dmwappushservice", "RetailDemo", "MapsBroker", "WalletService", "PhoneSvc",
        "WpcMonSvc", "Fax", "RemoteRegistry", "WMPNetworkSvc", "lfsvc", "SharedAccess", "TrkWks",
        "diagnosticshub.standardcollector.service", "wisvc", "SCardSvr", "ScDeviceEnum", "SEMgrSvc"
    ];

    private static object AddStartup(string path)
    {
        var (ok, message, id) = Startup.Add(path);
        return new { ok, message, id, cancelled = false };
    }

    private static string Group(SvcInfo s)
    {
        var group = SvcGroups.Of(s.Name);
        if (group != "other") return group;
        if (s.Driver) return "driver";
        return SvcGroups.Foreign(s.Image) ? "thirdparty" : "other";
    }

    private static object ServiceList()
    {
        var all = Svc.All();
        var journalDisabled = Journal.Entries.Where(x => !x.Reverted).SelectMany(x => x.Items).Where(x => x.Kind == "svc").Select(x => x.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stock = Repair.Db.ServiceDefaults;

        return all.Select(s =>
        {
            var known = stock.TryGetValue(s.Name, out var mode) ? mode : null;
            return new
            {
                s.Name,
                s.Display,
                s.Desc,
                s.Start,
                s.Status,
                recommended = SafeToDisable.Contains(s.Name, StringComparer.OrdinalIgnoreCase),
                touched = journalDisabled.Contains(s.Name),
                stock = known,
                changed = known is not null && !string.Equals(known, s.Start, StringComparison.OrdinalIgnoreCase),
                group = Group(s),
                driver = s.Driver
            };
        });
    }

    private static object ServiceSet(JsonElement? p)
    {
        var name = S(p, "name");
        var mode = S(p, "mode", "manual");
        try
        {
            var entry = new JournalEntry { TweakId = "svc:" + name, Title = name, Group = "service" };
            entry.Items.Add(new JournalItem { Kind = "svc", Target = name, PrevValue = Svc.GetStart(name) });
            Svc.SetStart(name, mode);
            if (mode == "disabled") Svc.Stop(name);
            Journal.Add(entry);
            return new { ok = true, start = Svc.GetStart(name), status = Svc.GetStatus(name) };
        }
        catch (Exception ex) { return new { ok = false, error = ex.Message }; }
    }

    private static object ServiceControl(JsonElement? p)
    {
        var name = S(p, "name");
        var action = S(p, "action");
        var ok = action == "start" ? Svc.Start(name) : Svc.Stop(name);
        return new { ok, status = Svc.GetStatus(name) };
    }

    private static object AppxRemove(string[] names, Push push)
    {
        var results = new List<object>();
        int done = 0;
        foreach (var n in names)
        {
            var r = Appx.Remove(n);
            done++;
            push("progress", new { stage = n, percent = done * 100 / Math.Max(1, names.Length), total = names.Length, done });
            results.Add(new { name = n, result = r });
        }
        Appx.Invalidate();
        return results;
    }

    private static object NetTelemetry(bool block)
    {
        if (block) HostsFile.Block(NetworkTools.TelemetryHosts);
        else HostsFile.Unblock(NetworkTools.TelemetryHosts);
        return new { blocked = NetworkTools.TelemetryBlocked(), count = HostsFile.Current().Count };
    }

    private static object BenchRun(string label, Push push)
    {
        void Handler(string stage, int percent) => push("progress", new { stage, percent, total = 100, done = percent });
        Benchmark.Progress += Handler;
        try { return Benchmark.Run(label); }
        finally { Benchmark.Progress -= Handler; }
    }

    private static object Credits()
    {
        var json = Res.Text("data/credits.json");
        if (string.IsNullOrWhiteSpace(json)) return new { note = "", sources = Array.Empty<object>() };
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static object RepairRun(string id) => id switch
    {
        "services" => Repair.RestoreServices(),
        "defender" => Repair.RestoreDefender(),
        "updates" => Repair.RestoreUpdates(),
        "store" => Repair.ResetStore(),
        "search" => Repair.RebuildSearch(),
        "network" => Repair.RestoreNetwork(),
        "godmode" => Repair.OpenGodMode(),
        "dns" => Repair.FlushDns(),
        "memory" => MemoryTool(),
        "explorer" => ExplorerTool(),
        "ctxti" => ShellMenu.ToggleTi(),
        "ctxown" => ShellMenu.ToggleOwn(),
        _ => new RepairResult { Ok = false, Message = "unknown repair: " + id }
    };

    private static RepairResult MemoryTool()
    {
        var t = HwMonitor.TrimMemory();
        return new RepairResult { Changed = t.Trimmed, Message = "освобождено " + t.Freed / 1024 / 1024 + " МБ" };
    }

    private static RepairResult ExplorerTool()
    {
        Sh.Ps("Stop-Process -Name explorer -Force", 20000);
        return new RepairResult { Message = "проводник перезапущен" };
    }

    private static object UpdateInstall(string url, Push push)
    {
        var expected = $"https://github.com/{Updater.Owner}/{Updater.Repo}/releases/download/";
        if (!url.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
            return new { ok = false, message = "ссылка не принадлежит релизам Klondaik Tweaker" };

        try
        {
            push("update", new { stage = "download", percent = 0 });
            var file = Updater.Download(url, percent => push("update", new { stage = "download", percent }));
            push("update", new { stage = "install", percent = 100 });
            Updater.Install(file);
            Task.Run(async () => { await Task.Delay(1200); QuitRequested?.Invoke(); });
            return new { ok = true, restarting = true };
        }
        catch (Exception ex)
        {
            return new { ok = false, message = ex.Message };
        }
    }

    private static object Power(string action)
    {
        switch (action)
        {
            case "restart": Sh.Run("shutdown.exe", "/r /t 5 /c \"Klondaik Tweaker\"", 5000); break;
            case "logoff": Sh.Run("shutdown.exe", "/l", 5000); break;
            case "explorer": Sh.Ps("Stop-Process -Name explorer -Force", 20000); break;
            case "cancel": Sh.Run("shutdown.exe", "/a", 5000); break;
        }
        return new { ok = true };
    }
}
