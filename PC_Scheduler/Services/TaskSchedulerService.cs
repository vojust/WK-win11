using System.Diagnostics;
using PCScheduler.Models;

namespace PCScheduler.Services;

public class TaskSchedulerService
{
    private const string TaskPrefix = "PCScheduler_";

    public async Task CreateOrUpdateTask(ScheduleEntry entry)
    {
        await DeleteTask(entry);

        var taskName = $"{TaskPrefix}{entry.Id}";
        entry.TaskName = taskName;

        var timeStr = entry.Time.ToString(@"HH:mm");
        var daysModifier = GetDaysModifier(entry);

        string action;
        if (entry.Type == ScheduleType.Sleep)
        {
            action = $"schtasks /create /tn \"{taskName}\" /tr \"rundll32.exe powrprof.dll,SetSuspendState 0,1,0\" /sc daily /st {timeStr}{daysModifier} /f";
        }
        else
        {
            action = $"schtasks /create /tn \"{taskName}\" /tr \"exit\" /sc daily /st {timeStr}{daysModifier} /f /WAKE";
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = action,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process != null)
        {
            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode != 0)
                throw new Exception($"Ошибка создания задачи: {error}");
        }
    }

    public async Task DeleteTask(ScheduleEntry entry)
    {
        if (string.IsNullOrEmpty(entry.TaskName))
            return;

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/delete /tn \"{entry.TaskName}\" /f",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process != null)
        {
            await process.WaitForExitAsync();
        }

        entry.TaskName = "";
    }

    public async Task EnableTask(ScheduleEntry entry)
    {
        if (string.IsNullOrEmpty(entry.TaskName))
        {
            await CreateOrUpdateTask(entry);
            return;
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/change /tn \"{entry.TaskName}\" /enable",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process != null)
            await process.WaitForExitAsync();
    }

    public async Task DisableTask(ScheduleEntry entry)
    {
        if (string.IsNullOrEmpty(entry.TaskName))
            return;

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/change /tn \"{entry.TaskName}\" /disable",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process != null)
            await process.WaitForExitAsync();
    }

    public async Task ApplyAll(IEnumerable<ScheduleEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Enabled)
                await CreateOrUpdateTask(entry);
            else
                await DeleteTask(entry);
        }
    }

    public async Task CleanupStaleTasks(IEnumerable<ScheduleEntry> activeEntries)
    {
        var activeIds = new HashSet<string>(activeEntries.Select(e => e.Id));

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/query /fo csv /nh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process == null) return;

        var output = await process.StandardOutput.ReadToEndAsync();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Trim('"').Split('"');
            if (parts.Length < 1) continue;

            var taskName = parts[0].Trim('"').Trim();
            if (taskName.StartsWith(TaskPrefix))
            {
                var id = taskName[TaskPrefix.Length..];
                if (!activeIds.Contains(id))
                {
                    var delProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/delete /tn \"{taskName}\" /f",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    if (delProcess != null)
                        await delProcess.WaitForExitAsync();
                }
            }
        }
    }

    private static string GetDaysModifier(ScheduleEntry entry)
    {
        return entry.Repeat switch
        {
            RepeatType.Daily => "",
            RepeatType.Weekdays => " /d MON,TUE,WED,THU,FRI",
            RepeatType.Weekly when entry.SelectedDays.Count > 0 =>
                " /d " + string.Join(",", entry.SelectedDays.Select(d => d.ToString()[..3].ToUpper())),
            RepeatType.Once => "",
            _ => ""
        };
    }
}
