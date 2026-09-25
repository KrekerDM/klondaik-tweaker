using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Modules;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Host;

public static class ModuleTest
{
    public static void Run(string outPath)
    {
        var report = new StringBuilder();
        var results = new List<object>();
        var f = Env.Facts;

        report.AppendLine("Klondaik Tweaker " + (typeof(ModuleTest).Assembly.GetName().Version?.ToString(3) ?? "1.0.0") + " - module execution test");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine($"{f.OsName} / {f.OsVersion} / {f.RamGb} GB / tier={f.Tier}");
        report.AppendLine("Every module below is actually executed. Side effects are real.");
        report.AppendLine(new string('-', 78));

        void Flush()
        {
            try
            {
                File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
                File.WriteAllText(Path.ChangeExtension(outPath, ".json"), JsonSerializer.Serialize(results, Store.Options));
            }
            catch { }
        }

        void Step(string name, Func<object?> action, string? note = null)
        {
            report.AppendLine($"[ .. ] {name} started");
            Flush();
            var sw = Stopwatch.StartNew();
            try
            {
                var value = action();
                sw.Stop();
                var json = JsonSerializer.Serialize(value, Store.Options);
                if (json.Length > 300) json = json[..300] + "...";
                results.Add(new { name, ok = true, ms = sw.ElapsedMilliseconds, result = json });
                report.Remove(report.Length - ($"[ .. ] {name} started" + Environment.NewLine).Length, ($"[ .. ] {name} started" + Environment.NewLine).Length);
                report.AppendLine($"[ OK ] {name} ({sw.ElapsedMilliseconds} ms)");
                report.AppendLine("        " + json);
                if (note is not null) report.AppendLine("        " + note);
                Flush();
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new { name, ok = false, ms = sw.ElapsedMilliseconds, error = ex.Message });
                report.Remove(report.Length - ($"[ .. ] {name} started" + Environment.NewLine).Length, ($"[ .. ] {name} started" + Environment.NewLine).Length);
                report.AppendLine($"[FAIL] {name} ({sw.ElapsedMilliseconds} ms): {ex.GetType().Name}: {ex.Message}");
                Flush();
            }
        }

        void Noop(string channel, object data) { }

        Step("monitor.trim", () => { var t = HwMonitor.TrimMemory(); return new { t.Trimmed, freedMb = t.Freed / 1024 / 1024 }; });
        Step("monitor.top", () => HwMonitor.TopProcesses(5).Count);
        Step("repair.dns", () => Repair.FlushDns());
        Step("repair.godmode", () => Repair.OpenGodMode());

        Step("restore.enable", () => new { result = RestorePoint.Enable(), enabled = RestorePoint.Enabled() });
        Step("restore.create", () => RestorePoint.Create("Klondaik module test"));
        Step("restore.list", () => RestorePoint.List().Count);

        Step("clean.scan", () =>
        {
            var targets = Cleaner.Scan();
            return new { count = targets.Count, totalMb = targets.Sum(x => x.Bytes) / 1024 / 1024 };
        });
        Step("clean.run", () =>
        {
            var ids = new[] { "temp.user", "temp.win", "logs", "crash", "thumbs" };
            var r = Cleaner.Clean(ids);
            return new { freedMb = r.Freed / 1024 / 1024, r.Files, errors = r.Errors.Count };
        });

        Step("bench.run", () =>
        {
            var b = Benchmark.Run("module test", includeDisk: true);
            return new { b.Score, b.CpuSingle, b.CpuMulti, b.RamGbs, b.DiskWrite, b.DiskRead, b.LatencyAvg };
        });
        Step("bench.history", () => Benchmark.History().Count);

        Step("boost.start", () =>
        {
            var st = GameBoost.Start();
            return new { st.Active, services = st.Services.Count, freedMb = st.FreedRam / 1024 / 1024 };
        });
        Step("boost.stop", () => new { GameBoost.Stop().Active });

        Step("appx.list", () => Appx.List(true).Count);
        Step("appx.remove", () =>
        {
            var target = Appx.List().FirstOrDefault(x => x.Name.Contains("BingWeather", StringComparison.OrdinalIgnoreCase)
                                                     || x.Name.Contains("BingNews", StringComparison.OrdinalIgnoreCase)
                                                     || x.Name.Contains("MicrosoftSolitaire", StringComparison.OrdinalIgnoreCase));
            if (target is null) return new { skipped = true, reason = "no removable sample package present" };
            var before = Appx.List().Count;
            var result = Appx.Remove(target.Name);
            var after = Appx.List(true).Count;
            return new { package = target.Name, result, before, after };
        });

        Step("repair.defender", () => Repair.RestoreDefender());
        Step("repair.updates", () => Repair.RestoreUpdates());
        Step("repair.search", () => Repair.RebuildSearch());
        Step("repair.services", () => { var r = Repair.RestoreServices(); return new { r.Changed, r.Skipped, r.Message }; });
        Step("repair.store", () => { var r = Repair.ResetStore(); return new { r.Changed, r.Message, details = r.Details.Count }; });

        Step("features.list", () => { var f = Features.List("ru"); return new { count = f.Count, enabled = f.Count(x => x.State == "enabled") }; });
        Step("features.toggle", () =>
        {
            var target = Features.List("ru").FirstOrDefault(x => x.Rec == "off" && x.State == "enabled" && x.Known);
            if (target is null) return new { skipped = true, reason = "no safe-to-disable feature is enabled here" };
            var off = Features.Set(target.Name, false);
            var mid = Features.List("ru", true).FirstOrDefault(x => x.Name == target.Name)?.State;
            var on = Features.Set(target.Name, true);
            var back = Features.List("ru", true).FirstOrDefault(x => x.Name == target.Name)?.State;
            return new { offOk = off.Ok, mid, onOk = on.Ok, back, roundTrip = mid == "disabled" && back == "enabled" };
        }, "a safe-to-disable feature is switched off and back on");

        Step("tasks.list", () => { var g = TaskGroups.List("ru"); return new { groups = g.Count, tasks = g.Sum(x => x.Tasks.Count), enabled = g.Sum(x => x.Enabled) }; });
        Step("tasks.roundTrip", () =>
        {
            var group = TaskGroups.List("ru").FirstOrDefault(x => x.Id == "insider" && x.Tasks.Count > 0)
                        ?? TaskGroups.List("ru").FirstOrDefault(x => x.Rec == "off" && x.Tasks.Count > 0);
            if (group is null) return new { skipped = true, reason = "no removable task group present" };
            var before = TaskGroups.List("ru").First(x => x.Id == group.Id).Enabled;
            var off = TaskGroups.SetGroup(group.Id, false, "ru");
            var mid = TaskGroups.List("ru").First(x => x.Id == group.Id).Enabled;
            var on = TaskGroups.SetGroup(group.Id, true, "ru");
            var after = TaskGroups.List("ru").First(x => x.Id == group.Id).Enabled;
            return new { group = group.Id, before, mid, after, restored = before == after, offOk = off.Ok, onOk = on.Ok };
        }, "a task group is switched off and back on");

        Step("power.state", () => new
        {
            schemes = PowerPlans.Schemes().Count,
            active = PowerPlans.Schemes().Count(x => x.Active),
            hidden = PowerPlans.HiddenCount(),
            files = PowerPlans.Files().Count
        });

        Step("nic.params", () =>
        {
            var adapters = Nic.Adapters();
            if (adapters.Count == 0) return new { skipped = true, reason = "no adapter exposes parameters" };
            var first = adapters[0];
            var list = Nic.Params(first.Id);
            return new { adapter = first.Name, adapters = adapters.Count, parameters = list.Count, edited = list.Count(x => x.Edited) };
        });

        Step("clean.uwpLeftovers", () =>
        {
            var target = Cleaner.Scan().FirstOrDefault(x => x.Id == "uwp.leftover");
            if (target is null) return new { skipped = true, reason = "target missing" };
            return new { folders = target.Files, megabytes = target.Bytes / 1024 / 1024 };
        });

        Step("startup.addRoundTrip", () =>
        {
            var exe = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            if (!File.Exists(exe)) return new { skipped = true, reason = "notepad missing" };

            var junk = Path.Combine(Path.GetTempPath(), "klondaik-not-a-program.txt");
            File.WriteAllText(junk, "x");
            var refused = Startup.Add(junk);
            File.Delete(junk);

            var (ok, message, id) = Startup.Add(exe);
            if (!ok || id is null) throw new Exception("не добавилось: " + message);

            var listed = Startup.List().FirstOrDefault(x => x.Id == id);
            var deleted = Startup.Delete(id);
            var gone = Startup.List().All(x => x.Id != id);

            if (!deleted || !gone) throw new Exception("запись осталась в автозагрузке");
            return new { added = listed?.Name, command = listed?.Command, junkRefused = !refused.Ok, junkReason = refused.Message, deleted, gone };
        }, "программа кладётся в автозагрузку и убирается обратно");

        Step("services.groups", () =>
        {
            var stock = Repair.Db.ServiceDefaults;
            var all = Svc.All();
            var named = all.Count(x => SvcGroups.Of(x.Name) != "other");
            var drivers = all.Count(x => x.Driver);
            var foreign = all.Count(x => !x.Driver && SvcGroups.Foreign(x.Image));
            var top = all.GroupBy(x => SvcGroups.Of(x.Name))
                .Where(g => g.Key != "other")
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key + "=" + g.Count());
            return new { services = all.Count, drivers, grouped = named, thirdParty = foreign, biggest = string.Join(" ", top) };
        });

        Step("services.changed", () =>
        {
            var stock = Repair.Db.ServiceDefaults;
            var all = Svc.All();
            var known = all.Where(x => stock.ContainsKey(x.Name)).ToList();
            var off = known.Count(x => !string.Equals(stock[x.Name], x.Start, StringComparison.OrdinalIgnoreCase));
            return new { services = all.Count, known = known.Count, changed = off };
        });

        Step("ghosts.list", () =>
        {
            var list = Ghosts.List();
            return new { found = list.Count, removable = list.Count(x => x.Removable), classes = list.Select(x => x.Class).Distinct().Count() };
        });

        Step("nic.roundTrip", () =>
        {
            var adapter = Nic.Adapters().FirstOrDefault();
            if (adapter is null) return new { skipped = true, reason = "no adapter exposes parameters" };

            var target = Nic.Params(adapter.Id)
                .FirstOrDefault(x => !x.Risky && x.Type == "enum" && x.Options.Count >= 2);
            if (target is null) return new { skipped = true, reason = "no safe enumeration parameter to flip" };

            var original = target.Current;
            var other = target.Options.First(x => !x.Value.Equals(original, StringComparison.OrdinalIgnoreCase)).Value;

            var set = Nic.Set(adapter.Id, target.Name, other);
            var mid = Nic.Params(adapter.Id).First(x => x.Name == target.Name).Current;
            var back = Nic.Set(adapter.Id, target.Name, original);
            var after = Nic.Params(adapter.Id).First(x => x.Name == target.Name).Current;

            return new
            {
                adapter = adapter.Name,
                parameter = target.Desc,
                original,
                mid,
                after,
                setOk = set.Ok,
                backOk = back.Ok,
                restored = after == original && mid == other
            };
        }, "one safe adapter parameter is changed and put back");

        Step("power.unhideRoundTrip", () =>
        {
            var before = PowerPlans.HiddenCount();
            if (before == 0) return new { skipped = true, reason = "nothing is hidden on this system" };

            var unhide = PowerPlans.Unhide();
            var mid = PowerPlans.HiddenCount();

            var entry = Journal.Entries.LastOrDefault(x => x.TweakId == "power.unhide" && !x.Reverted);
            var reverted = 0;
            if (entry is not null)
            {
                foreach (var item in entry.Items)
                {
                    try { TweakEngine.RevertItem(item); reverted++; }
                    catch { }
                }
            }
            var after = PowerPlans.HiddenCount();

            return new
            {
                before,
                mid,
                after,
                unhideOk = unhide.Ok,
                changed = unhide.Changed,
                reverted,
                restored = mid == 0 && after == before
            };
        }, "hidden power settings are revealed and hidden again through the journal");

        Step("irq.topology", () =>
        {
            var threads = Cpu.Threads();
            return new
            {
                threads = threads.Count,
                cores = threads.Select(x => x.Core).Distinct().Count(),
                hybrid = Cpu.Hybrid(),
                performance = threads.Count(x => x.Performance)
            };
        });

        Step("irq.roundTrip", () =>
        {
            var free = Irq.Devices().FirstOrDefault(x => !x.Bound && x.Kind != "storage")
                       ?? Irq.Devices().FirstOrDefault(x => !x.Bound);
            if (free is null) return new { skipped = true, reason = "every device already has an affinity set" };

            var bind = Irq.Bind(free.Id, [0], false);
            var afterBind = Irq.Devices().First(x => x.Id == free.Id);
            var reset = Irq.Reset(free.Id);
            var afterReset = Irq.Devices().First(x => x.Id == free.Id);

            return new
            {
                device = free.Name,
                bindOk = bind.Ok,
                boundMask = afterBind.MaskHex,
                boundPolicy = afterBind.Policy,
                resetOk = reset.Ok,
                policyAfter = afterReset.Policy,
                restored = !afterReset.Bound && afterReset.Policy == free.Policy
            };
        }, "a device is pinned to thread 0 and unpinned again");

        Step("update.auto", () =>
        {
            Settings.Update(s => s.LastUpdateCheck = null);
            var first = Api.Handle("update.auto", null, (_, _) => { });
            var second = Api.Handle("update.auto", null, (_, _) => { });
            var stamp = Settings.Data.LastUpdateCheck;
            var json = System.Text.Json.JsonSerializer.Serialize(second, Store.Options);
            if (stamp is null) throw new Exception("время проверки не записалось в настройки");
            if (!json.Contains("recent")) throw new Exception("вторая проверка не отсеклась по времени: " + json);
            return new { stamped = stamp, secondCall = json };
        }, "обновления проверяются не чаще раза в двадцать часов");

        Step("soft.winget", () => SoftCatalog.HasWinget());
        Step("api.router", () =>
        {
            var probes = new[] { "app.info", "tweaks.presets", "services.list", "startup.list", "net.adapters", "journal.list", "app.credits" };
            var ok = 0;
            foreach (var m in probes)
            {
                try { if (Api.Handle(m, null, Noop) is not null) ok++; }
                catch { }
            }
            return new { probed = probes.Length, ok };
        });

        Step("repair.network", () => { var r = Repair.RestoreNetwork(); return new { r.Changed, r.Message }; }, "network stack reset runs last");

        var failed = results.Count(x => x.GetType().GetProperty("ok")?.GetValue(x) is false);
        report.AppendLine();
        report.AppendLine($"journal entries left active: {Journal.Entries.Count(x => !x.Reverted)}");
        report.AppendLine($"{results.Count - failed}/{results.Count} passed");

        try
        {
            File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.ChangeExtension(outPath, ".json"), JsonSerializer.Serialize(results, Store.Options));
        }
        catch { }
    }
}
