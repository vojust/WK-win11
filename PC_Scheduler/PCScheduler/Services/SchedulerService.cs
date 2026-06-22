using System.Diagnostics;
using PCScheduler.Core;

namespace PCScheduler.Services;

public static class SchedulerService
{
    const string Prefix = "PCSched_";

    static string Run(string[] args)
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

        if (proc.ExitCode != 0)
        {
            var err = (proc.StandardError?.ReadToEnd() ?? "").Trim();
            var cmd = "schtasks.exe " + string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            throw new Exception($"[{cmd}] {err}");
        }

        return (proc.StandardOutput?.ReadToEnd() ?? "").Trim();
    }

    public static void Delete(string taskName)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("/delete");
        psi.ArgumentList.Add("/tn");
        psi.ArgumentList.Add(taskName);
        psi.ArgumentList.Add("/f");

        var proc = Process.Start(psi);
        if (proc == null) return;
        proc.WaitForExit();

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

        var args = new List<string> { "/create", "/tn", name };

        if (entry.Type == ScheduleType.Wake)
        {
            args.Add("/tr"); args.Add("exit");
            args.Add("/WAKE");
        }
        else
        {
            var flag = entry.Type == ScheduleType.Hibernate ? "1" : "0";
            args.Add("/tr"); args.Add($"rundll32.exe powrprof.dll,SetSuspendState {flag},1,0");
        }

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
        }

        args.Add("/f");

        if (entry.Repeat == RepeatType.Weekdays)
        {
            args.Add("/d"); args.Add("MON,TUE,WED,THU,FRI");
        }
        else if (entry.Repeat == RepeatType.Weekly && entry.Days.Count > 0)
        {
            args.Add("/d"); args.Add(string.Join(",", entry.Days));
        }

        Run(args.ToArray());
    }

    public static void Remove(ScheduleEntry entry) => Delete($"{Prefix}{entry.Id}");

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
        var active = new HashSet<string>(entries.Select(e => $"{Prefix}{e.Id}"));

        try
        {
            var lines = QueryRawTasks();
            foreach (var tn in lines)
            {
                if (tn.StartsWith(Prefix) && !active.Contains(tn))
                {
                    try { Run(new[] { "/delete", "/tn", tn, "/f" }); }
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
                    try { Run(new[] { "/delete", "/tn", tn, "/f" }); }
                    catch { }
                }
            }
        }
        catch { }
    }

    static List<string> QueryRawTasks()
    {
        var result = new List<string>();
        var outLines = Run(new[] { "/query", "/fo", "csv", "/nh" });
        foreach (var line in outLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(new[] { "\",\"" }, StringSplitOptions.None);
            if (parts.Length < 2) continue;
            result.Add(parts[0].Trim('"').Trim());
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
        Run(new[]
        {
            "/create", "/tn", tag,
            "/tr", "exit",
            "/sc", "once",
            "/st", $"{time:HH:mm}",
            "/sd", $"{time:yyyy/MM/dd}",
            "/f", "/WAKE"
        });
    }
}
