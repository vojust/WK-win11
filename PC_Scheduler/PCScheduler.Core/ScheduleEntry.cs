using System.Text.Json.Serialization;

namespace PCScheduler.Core;

public enum ScheduleType { Sleep, Wake }

public enum RepeatType { Daily, Weekdays, Weekly, Once }

public class ScheduleEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public ScheduleType Type { get; set; } = ScheduleType.Sleep;
    public string Time { get; set; } = "08:00";
    public RepeatType Repeat { get; set; } = RepeatType.Daily;
    public bool Enabled { get; set; } = true;
    public List<string> Days { get; set; } = new();

    [JsonIgnore] public string TypeDisplay => Type == ScheduleType.Sleep ? "Сон" : "Пробуждение";
    [JsonIgnore] public string RepeatDisplay => Repeat switch
    {
        RepeatType.Daily => "Ежедневно",
        RepeatType.Weekdays => "По будням",
        RepeatType.Weekly when Days.Count > 0 => $"Еженедельно ({string.Join(", ", Days.Select(DayNameRu))})",
        RepeatType.Once => "Один раз",
        _ => ""
    };
    [JsonIgnore] public string StatusDisplay => Enabled ? "✓" : "✗";

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
