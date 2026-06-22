using System.Windows;
using PCScheduler.Core;

namespace PCScheduler;

public partial class ScheduleDialog : Window
{
    public ScheduleEntry? Result { get; private set; }

    static readonly ScheduleType[] TypeMap = { ScheduleType.Sleep, ScheduleType.Hibernate, ScheduleType.Wake };

    public ScheduleDialog(Window owner, ScheduleEntry? entry = null)
    {
        InitializeComponent();
        Owner = owner;

        if (entry != null)
        {
            Title = "Редактирование";
            TypeCombo.SelectedIndex = Array.IndexOf(TypeMap, entry.Type);
            TimeBox.Text = entry.TimeFormatted;
            RepeatCombo.SelectedIndex = (int)entry.Repeat;
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
        var time = TimeBox.Text.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(time, @"^\d{1,2}:\d{2}$"))
        {
            MessageBox.Show(this, "Введите время в формате ЧЧ:ММ", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TimeBox.Focus();
            return;
        }

        var parts = time.Split(':');
        var h = int.Parse(parts[0]);
        var m = int.Parse(parts[1]);
        if (h > 23 || m > 59)
        {
            MessageBox.Show(this, "Введите корректное время (0-23:0-59)", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show(this, "Выберите хотя бы один день недели", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new ScheduleEntry
        {
            Time = $"{h:D2}:{m:D2}",
            Type = TypeCombo.SelectedIndex >= 0 && TypeCombo.SelectedIndex < TypeMap.Length
                ? TypeMap[TypeCombo.SelectedIndex] : ScheduleType.Sleep,
            Repeat = (RepeatType)RepeatCombo.SelectedIndex,
            Days = days,
            Enabled = true,
        };

        DialogResult = true;
    }
}
