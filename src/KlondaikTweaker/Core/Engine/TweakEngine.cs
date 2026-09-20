using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Modules;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Engine;

public static class TweakEngine
{
    private static readonly string[] Detectable = ["reg", "regdel", "svc", "task", "appx"];
    private static readonly char[] Sep = ['\\'];

    public static TweakState Detect(TweakDef t) => Detect(t, out _);

    public static TweakState Detect(TweakDef t, out string? reason)
    {
        reason = null;
        if (!Env.Meets(t.Req))
        {
            reason = "req:" + (t.Req ?? "");
            return TweakState.Unavailable;
        }

        int hits = 0, misses = 0, checkable = 0, detectable = 0;
        foreach (var a in t.Actions)
        {
            if (!Detectable.Contains(a.K)) continue;
            detectable++;
            checkable++;
            bool applied;
            switch (a.K)
            {
                case "reg":
                {
                    var r = Reg.Read(a.H!, a.P!, a.N);
                    applied = r.Exists && Reg.Same(a.T, a.V ?? "", r.Value);
                    break;
                }
                case "regdel":
                {
                    applied = string.IsNullOrEmpty(a.N)
                        ? !Reg.KeyExists(a.H!, a.P!)
                        : !Reg.Read(a.H!, a.P!, a.N).Exists;
                    break;
                }
                case "svc":
                {
                    if (!Svc.Exists(a.N!)) { checkable--; continue; }
                    applied = string.Equals(Svc.GetStart(a.N!), a.V, StringComparison.OrdinalIgnoreCase);
                    break;
                }
                case "task":
                {
                    var e = Tasks.GetEnabled(a.P!);
                    if (e is null) { checkable--; continue; }
                    applied = e.Value == string.Equals(a.V, "on", StringComparison.OrdinalIgnoreCase);
                    break;
                }
                case "appx":
                {
                    applied = !Appx.Installed(a.N!);
                    break;
                }
                default: continue;
            }
            if (applied) hits++; else misses++;
        }

        if (checkable == 0 && detectable > 0)
        {
            reason = "missing";
            return TweakState.Unavailable;
        }
        if (checkable == 0) return Journal.IsApplied(t.Id) ? TweakState.Applied : TweakState.NotApplied;
        if (misses == 0) return TweakState.Applied;
        if (hits == 0) return TweakState.NotApplied;
        return TweakState.Partial;
    }

    public static ActionResult Apply(TweakDef t)
    {
        var res = new ActionResult { NeedsRestart = t.Restart };
        if (!Env.Meets(t.Req)) return new ActionResult { Ok = false, Error = "unsupported" };

        var entry = new JournalEntry { TweakId = t.Id, Title = t.Ru.T, Group = "tweak" };
        var errors = new List<string>();

        foreach (var a in t.Actions)
        {
            try
            {
                switch (a.K)
                {
                    case "reg":
                    {
                        var prev = Reg.Read(a.H!, a.P!, a.N);
                        entry.Items.Add(new JournalItem
                        {
                            Kind = a.Dk && !Reg.KeyExists(a.H!, a.P!) ? "regnewkey" : "reg",
                            Target = a.H + "\\" + a.P,
                            Name = a.N,
                            Existed = prev.Exists,
                            PrevType = prev.Exists ? prev.Kind : a.T,
                            PrevValue = prev.Exists ? prev.Value : a.D
                        });
                        Reg.Write(a.H!, a.P!, a.N, a.T, a.V ?? "");
                        break;
                    }
                    case "regdel":
                    {
                        if (string.IsNullOrEmpty(a.N))
                        {
                            entry.Items.Add(new JournalItem { Kind = "regkeydel", Target = a.H + "\\" + a.P, Existed = Reg.KeyExists(a.H!, a.P!) });
                            Reg.DeleteTree(a.H!, a.P!);
                        }
                        else
                        {
                            var prev = Reg.Read(a.H!, a.P!, a.N);
                            entry.Items.Add(new JournalItem { Kind = "reg", Target = a.H + "\\" + a.P, Name = a.N, Existed = prev.Exists, PrevType = prev.Kind, PrevValue = prev.Value });
                            Reg.DeleteValue(a.H!, a.P!, a.N);
                        }
                        break;
                    }
                    case "svc":
                    {
                        if (!Svc.Exists(a.N!)) break;
                        entry.Items.Add(new JournalItem { Kind = "svc", Target = a.N!, PrevValue = Svc.GetStart(a.N!) });
                        Svc.SetStart(a.N!, a.V ?? "manual");
                        if (a.Stop) Svc.Stop(a.N!);
                        break;
                    }
                    case "task":
                    {
                        var cur = Tasks.GetEnabled(a.P!);
                        if (cur is null) break;
                        entry.Items.Add(new JournalItem { Kind = "task", Target = a.P!, PrevValue = cur.Value ? "on" : "off" });
                        Tasks.SetEnabled(a.P!, string.Equals(a.V, "on", StringComparison.OrdinalIgnoreCase));
                        break;
                    }
                    case "appx":
                    {
                        if (!Appx.Installed(a.N!)) break;
                        entry.Items.Add(new JournalItem { Kind = "appx", Target = a.N!, PrevValue = "installed" });
                        Appx.Remove(a.N!);
                        break;
                    }
                    case "cmd":
                    {
                        var target = a.Exe!;
                        var revert = a.RArgs;
                        if (a.Cap == "powerplan")
                        {
                            var scheme = Win.Power.ActiveScheme();
                            if (scheme is not null)
                            {
                                target = "powercfg.exe";
                                revert = "/setactive " + scheme;
                            }
                        }
                        var r = a.Ti ? Ti.RunExe(a.Exe!, a.Args ?? "") : Sh.Run(a.Exe!, a.Args ?? "", 180000);
                        if (!r.Ok && !a.Ti) r = Ti.RunExe(a.Exe!, a.Args ?? "");
                        if (r.Ok) entry.Items.Add(new JournalItem { Kind = "cmd", Target = target, PrevValue = a.Args, Revert = revert });
                        else errors.Add(Path.GetFileName(a.Exe) + ": " + Trim(r.All));
                        break;
                    }
                    case "hosts":
                    {
                        entry.Items.Add(new JournalItem { Kind = "hosts", Target = a.V ?? "" });
                        HostsFile.Block((a.V ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries));
                        break;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                errors.Add("access denied: " + (a.P ?? a.N ?? a.Exe ?? a.K));
            }
            catch (Exception ex)
            {
                errors.Add(Trim(ex.Message));
            }
        }

        if (entry.Items.Count > 0) Journal.Add(entry);

        if (errors.Count > 0)
        {
            res.Ok = errors.Count < t.Actions.Length;
            res.Error = string.Join("; ", errors.Distinct().Take(3));
        }
        return res;
    }

    public static ActionResult Revert(TweakDef t)
    {
        var res = new ActionResult { NeedsRestart = t.Restart };
        var entry = Journal.Latest(t.Id);
        var errors = new List<string>();

        if (entry is not null)
        {
            for (int i = entry.Items.Count - 1; i >= 0; i--)
            {
                try { RevertItem(entry.Items[i]); }
                catch (Exception ex) { errors.Add(Trim(ex.Message)); }
            }
            Journal.MarkReverted(entry.Id);
        }
        else
        {
            foreach (var a in t.Actions)
            {
                try
                {
                    switch (a.K)
                    {
                        case "reg":
                            if (a.Dk) Reg.DeleteTree(a.H!, a.P!);
                            else if (a.D is null) Reg.DeleteValue(a.H!, a.P!, a.N);
                            else Reg.Write(a.H!, a.P!, a.N, a.T, a.D);
                            break;
                        case "svc":
                            if (a.D is not null && Svc.Exists(a.N!)) Svc.SetStart(a.N!, a.D);
                            break;
                        case "task":
                            Tasks.SetEnabled(a.P!, !string.Equals(a.V, "on", StringComparison.OrdinalIgnoreCase));
                            break;
                        case "cmd":
                            if (a.RArgs is not null) Sh.Run(a.Exe!, a.RArgs, 180000);
                            break;
                        case "hosts":
                            HostsFile.Unblock((a.V ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries));
                            break;
                    }
                }
                catch (Exception ex) { errors.Add(Trim(ex.Message)); }
            }
        }

        if (errors.Count > 0)
        {
            res.Ok = false;
            res.Error = string.Join("; ", errors.Distinct().Take(3));
        }
        return res;
    }

    public static void RevertItem(JournalItem it)
    {
        switch (it.Kind)
        {
            case "reg":
            {
                var parts = it.Target.Split(Sep, 2);
                if (parts.Length != 2) return;
                if (it.Existed && it.PrevValue is not null) Reg.Write(parts[0], parts[1], it.Name, it.PrevType, it.PrevValue);
                else Reg.DeleteValue(parts[0], parts[1], it.Name);
                break;
            }
            case "regnewkey":
            {
                var parts = it.Target.Split(Sep, 2);
                if (parts.Length != 2) return;
                Reg.DeleteTree(parts[0], parts[1]);
                break;
            }
            case "svc":
                if (it.PrevValue is not null && Svc.Exists(it.Target)) Svc.SetStart(it.Target, it.PrevValue);
                break;
            case "task":
                Tasks.SetEnabled(it.Target, it.PrevValue == "on");
                break;
            case "cmd":
                if (!string.IsNullOrEmpty(it.Revert)) Sh.Run(it.Target, it.Revert, 180000);
                break;
            case "hosts":
                HostsFile.Unblock(it.Target.Split('|', StringSplitOptions.RemoveEmptyEntries));
                break;
        }
    }

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 180 ? s[..180] : s;
    }

    public static TweakView ToView(TweakDef t, string lang, TweakState? state = null)
    {
        var loc = lang == "en" ? t.En : t.Ru;
        var alt = lang == "en" ? t.Ru : t.En;
        string? reason = null;
        var st = state ?? Detect(t, out reason);
        return new TweakView
        {
            Id = t.Id,
            Cat = t.Cat,
            Risk = t.Risk,
            Title = loc.T.Length > 0 ? loc.T : alt.T,
            Desc = loc.D.Length > 0 ? loc.D : alt.D,
            Warn = loc.W ?? alt.W,
            Tags = t.Tags,
            Src = t.Src,
            Restart = t.Restart,
            Logoff = t.Logoff,
            State = st.ToString().ToLowerInvariant(),
            Available = st != TweakState.Unavailable,
            Note = st == TweakState.Unavailable ? reason : null
        };
    }
}
