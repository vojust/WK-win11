using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PCScheduler.Models;

public enum ScheduleType
{
    Sleep,
    Wake
}

public enum RepeatType
{
    Daily,
    Weekdays,
    Weekly,
    Once
}

public class ScheduleEntry : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N")[..8];
    private ScheduleType _type = ScheduleType.Sleep;
    private TimeSpan _time = new(8, 0, 0);
    private RepeatType _repeat = RepeatType.Daily;
    private bool _enabled = true;
    private string _taskName = "";
    private ObservableCollection<DayOfWeek> _selectedDays = new();

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public ScheduleType Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeName)); }
    }

    public TimeSpan Time
    {
        get => _time;
        set { _time = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeDisplay)); }
    }

    public RepeatType Repeat
    {
        get => _repeat;
        set { _repeat = value; OnPropertyChanged(); OnPropertyChanged(nameof(RepeatDisplay)); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusGlyph)); }
    }

    [JsonIgnore]
    public string TaskName
    {
        get => _taskName;
        set { _taskName = value; OnPropertyChanged(); }
    }

    public ObservableCollection<DayOfWeek> SelectedDays
    {
        get => _selectedDays;
        set { _selectedDays = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public string TypeName => Type == ScheduleType.Sleep ? "Сон" : "Пробуждение";

    [JsonIgnore]
    public string TimeDisplay => Time.ToString(@"hh\:mm");

    [JsonIgnore]
    public string RepeatDisplay => Repeat switch
    {
        RepeatType.Daily => "Ежедневно",
        RepeatType.Weekdays => "По будням",
        RepeatType.Weekly => "Еженедельно",
        RepeatType.Once => "Один раз",
        _ => ""
    };

    [JsonIgnore]
    public string StatusGlyph => Enabled ? "✓" : "✗";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
