using System.Text.Json.Serialization;

namespace PCScheduler.Core;

[JsonConverter(typeof(ScheduleTypeConverter))]
public enum ScheduleType { Sleep, Hibernate, Wake }

public enum RepeatType { Daily, Weekdays, Weekly, Once }

public class ScheduleEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public ScheduleType Type { get; set; } = ScheduleType.Sleep;
    public string Time { get; set; } = "08:00";
    public RepeatType Repeat { get; set; } = RepeatType.Daily;
    public bool Enabled { get; set; } = true;
    public bool WarnBeforeSleep { get; set; }
    public List<string> Days { get; set; } = new();

    [JsonIgnore] public string TypeDisplay => Type switch
    {
        ScheduleType.Sleep => "Сон",
        ScheduleType.Hibernate => "Гибернация",
        ScheduleType.Wake => "Пробуждение",
        _ => ""
    };
    [JsonIgnore] public string RepeatDisplay => Repeat switch
    {
        RepeatType.Daily => "Ежедневно",
        RepeatType.Weekdays => "По будням",
        RepeatType.Weekly when Days.Count > 0 => $"Еженедельно ({string.Join(", ", Days.Select(DayNameRu))})",
        RepeatType.Once => "Один раз",
        _ => ""
    };
    [JsonIgnore] public string StatusDisplay => Enabled ? "✓" : "✗";
    [JsonIgnore] public string TimeUntilDisplay
    {
        get
        {
            if (!Enabled) return "";
            var next = GetNextTime();
            if (next == null) return "";
            var diff = next.Value - DateTime.Now;
            if (diff.TotalSeconds <= 0) return "сейчас";
            if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}д {diff.Hours:00}:{diff.Minutes:00}";
            return $"{(int)diff.TotalHours:00}:{diff.Minutes:00}:{diff.Seconds:00}";
        }
    }

    DateTime? GetNextTime()
    {
        var p = Time.Split(':');
        var h = int.Parse(p[0]);
        var m = int.Parse(p[1]);
        var now = DateTime.Now;
        var today = now.Date.AddHours(h).AddMinutes(m);

        if (Repeat == RepeatType.Daily)
            return today > now ? today : today.AddDays(1);

        if (Repeat == RepeatType.Weekdays)
        {
            var d = today > now ? today : today.AddDays(1);
            while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                d = d.AddDays(1);
            return d;
        }

        if (Repeat == RepeatType.Weekly && Days.Count > 0)
        {
            var dow = Days.Select(DayOfWeekFromAbbr).ToHashSet();
            var c = today > now ? today : today.AddDays(1);
            for (int i = 0; i < 14; i++)
            {
                if (dow.Contains(c.DayOfWeek)) return c;
                c = c.AddDays(1);
            }
            return null;
        }

        // Once
        return today > now ? today : today.AddDays(1);
    }

    static DayOfWeek DayOfWeekFromAbbr(string en) => en switch
    {
        "MON" => DayOfWeek.Monday, "TUE" => DayOfWeek.Tuesday,
        "WED" => DayOfWeek.Wednesday, "THU" => DayOfWeek.Thursday,
        "FRI" => DayOfWeek.Friday, "SAT" => DayOfWeek.Saturday,
        "SUN" => DayOfWeek.Sunday, _ => DayOfWeek.Monday,
    };

    private static string DayNameRu(string en) => en switch
    {
        "MON" => "Пн", "TUE" => "Вт", "WED" => "Ср",
        "THU" => "Чт", "FRI" => "Пт", "SAT" => "Сб", "SUN" => "Вс",
        _ => en
    };

    public string TimeFormatted
    {
        get
        {
            var parts = Time.Split(':');
            return $"{int.Parse(parts[0]):D2}:{int.Parse(parts[1]):D2}";
        }
    }
}
