using System.Diagnostics;
using PCScheduler.Core;

namespace PCScheduler.Services;

public static class SchedulerService
{
    const string Prefix = "PCSched_";
    const string WarnSuffix = "_warn";

    static readonly Dictionary<string, string> DayNames = new()
    {
        ["MON"] = "Monday", ["TUE"] = "Tuesday", ["WED"] = "Wednesday",
        ["THU"] = "Thursday", ["FRI"] = "Friday", ["SAT"] = "Saturday", ["SUN"] = "Sunday",
    };

    static string WarnName(string id) => $"{Prefix}{id}{WarnSuffix}";

    static string WarnTime(string time)
    {
        var p = time.Split(':');
        var total = int.Parse(p[0]) * 60 + int.Parse(p[1]) - 5;
        if (total < 0) total += 1440;
        return $"{total / 60:D2}:{total % 60:D2}";
    }

    static Process StartSchtasks(string[] args)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var proc = Process.Start(psi);
        if (proc == null) throw new Exception("Не удалось запустить schtasks.exe");
        proc.WaitForExit();
        return proc;
    }

    static string RunSchtasks(string[] args)
    {
        var proc = StartSchtasks(args);

        if (proc.ExitCode != 0)
        {
            var err = (proc.StandardError?.ReadToEnd() ?? "").Trim();
            var cmd = "schtasks.exe " + string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            throw new Exception($"[{cmd}] {err}");
        }

        return (proc.StandardOutput?.ReadToEnd() ?? "").Trim();
    }

    static void RunPowerShell(string script)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);

        var proc = Process.Start(psi);
        if (proc == null) throw new Exception("Не удалось запустить PowerShell");
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            var err = (proc.StandardError?.ReadToEnd() ?? "").Trim();
            throw new Exception($"PowerShell: {err}");
        }
    }

    public static void Delete(string taskName)
    {
        var proc = StartSchtasks(new[] { "/delete", "/tn", taskName, "/f" });

        if (proc.ExitCode != 0)
        {
            var err = (proc.StandardError?.ReadToEnd() ?? "").ToLowerInvariant();
            if (!err.Contains("не существует") && !err.Contains("does not exist") && !err.Contains("cannot find"))
                throw new Exception($"Ошибка удаления задачи: {err}");
        }
    }

    public static void CreateOrUpdate(ScheduleEntry entry)
    {
        var name = $"{Prefix}{entry.Id}";
        try { Delete(name); } catch { }
        try { Delete(WarnName(entry.Id)); } catch { }

        if (entry.Type == ScheduleType.Wake)
        {
            CreateWakeViaPowerShell(name, entry);
        }
        else
        {
            CreateSleepViaSchtasks(name, entry);
            if (entry.WarnBeforeSleep)
            {
                try { CreateWarnTask(entry); }
                catch { }
            }
        }
    }

    static void CreateSleepViaSchtasks(string name, ScheduleEntry entry)
    {
        var flag = entry.Type == ScheduleType.Hibernate ? "1" : "0";
        var args = new List<string>
        {
            "/create", "/tn", name,
            "/tr", $"rundll32.exe powrprof.dll,SetSuspendState {flag},1,0",
        };

        if (entry.Repeat == RepeatType.Once)
        {
            var today = DateTime.Now.ToString("yyyy/MM/dd");
            args.Add("/sc"); args.Add("once");
            args.Add("/st"); args.Add(entry.TimeFormatted);
            args.Add("/sd"); args.Add(today);
        }
        else
        {
            args.Add("/sc"); args.Add("daily");
            args.Add("/st"); args.Add(entry.TimeFormatted);
            if (entry.Repeat == RepeatType.Weekdays)
            {
                args.Add("/d"); args.Add("MON,TUE,WED,THU,FRI");
            }
            else if (entry.Repeat == RepeatType.Weekly && entry.Days.Count > 0)
            {
                args.Add("/d"); args.Add(string.Join(",", entry.Days));
            }
        }

        args.Add("/f");
        RunSchtasks(args.ToArray());
    }

    static void CreateWarnTask(ScheduleEntry entry)
    {
        var name = WarnName(entry.Id);
        var warnTime = WarnTime(entry.TimeFormatted);
        var msg = "Компьютер будет выключен через 5 минут";
        var taskRun = $"powershell.exe -NoProfile -Command \"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('{msg}','PCScheduler')\"";
        var args = new List<string>
        {
            "/create", "/tn", name,
            "/tr", taskRun,
        };

        if (entry.Repeat == RepeatType.Once)
        {
            var today = DateTime.Now.ToString("yyyy/MM/dd");
            args.Add("/sc"); args.Add("once");
            args.Add("/st"); args.Add(warnTime);
            args.Add("/sd"); args.Add(today);
        }
        else
        {
            args.Add("/sc"); args.Add("daily");
            args.Add("/st"); args.Add(warnTime);
            if (entry.Repeat == RepeatType.Weekdays)
            {
                args.Add("/d"); args.Add("MON,TUE,WED,THU,FRI");
            }
            else if (entry.Repeat == RepeatType.Weekly && entry.Days.Count > 0)
            {
                args.Add("/d"); args.Add(string.Join(",", entry.Days));
            }
        }

        args.Add("/f");
        RunSchtasks(args.ToArray());
    }

    static void CreateWakeViaPowerShell(string name, ScheduleEntry entry)
    {
        var time = entry.TimeFormatted;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("$action = New-ScheduledTaskAction -Execute 'exit'");

        if (entry.Repeat == RepeatType.Once)
        {
            sb.AppendLine($"$trigger = New-ScheduledTaskTrigger -Once -At \"{time}\"");
        }
        else if (entry.Repeat == RepeatType.Weekdays)
        {
            sb.AppendLine($"$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek \"Monday\",\"Tuesday\",\"Wednesday\",\"Thursday\",\"Friday\" -At \"{time}\"");
        }
        else if (entry.Repeat == RepeatType.Weekly && entry.Days.Count > 0)
        {
            var days = string.Join(",", entry.Days.Select(d => $"\"{DayNames.GetValueOrDefault(d, d)}\""));
            sb.AppendLine($"$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek {days} -At \"{time}\"");
        }
        else
        {
            sb.AppendLine($"$trigger = New-ScheduledTaskTrigger -Daily -At \"{time}\"");
        }

        sb.AppendLine("$settings = New-ScheduledTaskSettingsSet -WakeToRun");
        sb.AppendLine($"Register-ScheduledTask -TaskName \"{name}\" -Action $action -Trigger $trigger -Settings $settings -Force");

        RunPowerShell(sb.ToString());
    }

    public static void Remove(ScheduleEntry entry)
    {
        Delete($"{Prefix}{entry.Id}");
        try { Delete(WarnName(entry.Id)); } catch { }
    }

    public static void ApplyAll(List<ScheduleEntry> entries)
    {
        foreach (var e in entries)
        {
            if (e.Enabled) CreateOrUpdate(e);
            else Remove(e);
        }
        Cleanup(entries);
    }

    public static void Cleanup(List<ScheduleEntry> entries)
    {
        var active = new HashSet<string>();
        foreach (var e in entries)
        {
            active.Add($"{Prefix}{e.Id}");
            active.Add(WarnName(e.Id));
        }

        try
        {
            var lines = QueryRawTasks();
            foreach (var tn in lines)
            {
                if (tn.StartsWith(Prefix) && !active.Contains(tn))
                {
                    try { RunSchtasks(new[] { "/delete", "/tn", tn, "/f" }); }
                    catch { }
                }
            }
        }
        catch { }
    }

    public static void DeleteAll()
    {
        try
        {
            var lines = QueryRawTasks();
            foreach (var tn in lines)
            {
                if (tn.StartsWith(Prefix))
                {
                    try { RunSchtasks(new[] { "/delete", "/tn", tn, "/f" }); }
                    catch { }
                }
            }
        }
        catch { }
    }

    static List<string> QueryRawTasks()
    {
        var result = new List<string>();
        var csv = RunSchtasks(new[] { "/query", "/fo", "csv", "/nh" });
        if (string.IsNullOrWhiteSpace(csv)) return result;

        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            if (t.Length < 3 || !t.StartsWith('"')) continue;
            var end = t.IndexOf("\",\"", 1);
            if (end < 0) continue;
            result.Add(t[1..end]);
        }
        return result;
    }

    public static List<string> QueryActiveTasks()
    {
        try
        {
            return QueryRawTasks().Where(t => t.StartsWith(Prefix)).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void ScheduleTestWake()
    {
        var time = DateTime.Now.AddMinutes(2);
        var tag = $"{Prefix}test_{time:HHmm}";
        Delete(tag);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("$action = New-ScheduledTaskAction -Execute 'exit'");
        sb.AppendLine($"$trigger = New-ScheduledTaskTrigger -Once -At \"{time:HH:mm}\" -RepetitionDuration ([TimeSpan]::Zero)");
        sb.AppendLine("$settings = New-ScheduledTaskSettingsSet -WakeToRun");
        sb.AppendLine($"Register-ScheduledTask -TaskName \"{tag}\" -Action $action -Trigger $trigger -Settings $settings -Force");

        RunPowerShell(sb.ToString());
    }
}
