using System.Windows;
using PCScheduler.Core;

namespace PCScheduler;

public partial class ScheduleDialog : Window
{
    public ScheduleEntry? Result { get; private set; }

    static readonly ScheduleType[] TypeMap = { ScheduleType.Sleep, ScheduleType.Hibernate, ScheduleType.Wake };
    static readonly string[] Hours = Enumerable.Range(0, 24).Select(i => $"{i:D2}").ToArray();
    static readonly string[] Minutes = Enumerable.Range(0, 12).Select(i => $"{(i * 5):D2}").ToArray();

    public ScheduleDialog(Window owner, ScheduleEntry? entry = null)
    {
        InitializeComponent();
        Owner = owner;

        HourCombo.ItemsSource = Hours;
        MinCombo.ItemsSource = Minutes;

        TypeCombo.SelectionChanged += (_, _) => UpdateWarnVisibility();
        WarnCb.Visibility = Visibility.Collapsed;

        if (entry != null)
        {
            Title = "Редактирование";
            TypeCombo.SelectedIndex = Array.IndexOf(TypeMap, entry.Type);

            var parts = entry.TimeFormatted.Split(':');
            HourCombo.Text = parts[0];
            MinCombo.Text = parts[1];

            RepeatCombo.SelectedIndex = (int)entry.Repeat;
            WarnCb.IsChecked = entry.WarnBeforeSleep;
            MonCb.IsChecked = entry.Days.Contains("MON");
            TueCb.IsChecked = entry.Days.Contains("TUE");
            WedCb.IsChecked = entry.Days.Contains("WED");
            ThuCb.IsChecked = entry.Days.Contains("THU");
            FriCb.IsChecked = entry.Days.Contains("FRI");
            SatCb.IsChecked = entry.Days.Contains("SAT");
            SunCb.IsChecked = entry.Days.Contains("SUN");
            CheckDaysVisibility((int)entry.Repeat);
        }
    }

    void UpdateWarnVisibility()
    {
        WarnCb.Visibility = TypeCombo.SelectedIndex < 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    void CheckDaysVisibility(int idx)
    {
        DaysGroup.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    void OnRepeatChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        CheckDaysVisibility(RepeatCombo.SelectedIndex);
    }

    void OnSave(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HourCombo.Text, out var h) || h < 0 || h > 23)
        {
            System.Windows.MessageBox.Show(this, "Часы: введите число от 0 до 23", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            HourCombo.Focus();
            return;
        }

        if (!int.TryParse(MinCombo.Text, out var m) || m < 0 || m > 59)
        {
            System.Windows.MessageBox.Show(this, "Минуты: введите число от 0 до 59", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            MinCombo.Focus();
            return;
        }

        var days = new List<string>();
        if (MonCb.IsChecked == true) days.Add("MON");
        if (TueCb.IsChecked == true) days.Add("TUE");
        if (WedCb.IsChecked == true) days.Add("WED");
        if (ThuCb.IsChecked == true) days.Add("THU");
        if (FriCb.IsChecked == true) days.Add("FRI");
        if (SatCb.IsChecked == true) days.Add("SAT");
        if (SunCb.IsChecked == true) days.Add("SUN");

        if (RepeatCombo.SelectedIndex == 2 && days.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "Выберите хотя бы один день недели", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var type = TypeCombo.SelectedIndex >= 0 && TypeCombo.SelectedIndex < TypeMap.Length
            ? TypeMap[TypeCombo.SelectedIndex] : ScheduleType.Sleep;

        Result = new ScheduleEntry
        {
            Time = $"{h:D2}:{m:D2}",
            Type = type,
            Repeat = (RepeatType)RepeatCombo.SelectedIndex,
            Days = days,
            Enabled = true,
            WarnBeforeSleep = WarnCb.IsChecked == true && type != ScheduleType.Wake,
        };

        DialogResult = true;
    }
}
