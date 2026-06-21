using System.Windows;
using PCScheduler.Models;

namespace PCScheduler;

public partial class ScheduleDialog : Window
{
    public ScheduleEntry? Result { get; private set; }

    public ScheduleDialog(Window owner, ScheduleEntry? entry = null)
    {
        Owner = owner;
        InitializeComponent();

        if (entry != null)
            LoadEntry(entry);
    }

    private void LoadEntry(ScheduleEntry entry)
    {
        TypeCombo.SelectedIndex = entry.Type == ScheduleType.Sleep ? 0 : 1;
        TimeBox.Text = entry.Time.ToString(@"hh\:mm");
        RepeatCombo.SelectedIndex = (int)entry.Repeat;
        EnabledCb.IsChecked = entry.Enabled;

        MonCb.IsChecked = entry.SelectedDays.Contains(DayOfWeek.Monday);
        TueCb.IsChecked = entry.SelectedDays.Contains(DayOfWeek.Tuesday);
        WedCb.IsChecked = entry.SelectedDays.Contains(DayOfWeek.Wednesday);
        ThuCb.IsChecked = entry.SelectedDays.Contains(DayOfWeek.Thursday);
        FriCb.IsChecked = entry.SelectedDays.Contains(DayOfWeek.Friday);
        SatCb.IsChecked = entry.SelectedDays.Contains(DayOfWeek.Saturday);
        SunCb.IsChecked = entry.SelectedDays.Contains(DayOfWeek.Sunday);

        DaysPanel.IsEnabled = entry.Repeat == RepeatType.Weekly;
    }

    private void RepeatCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        DaysPanel.IsEnabled = RepeatCombo.SelectedIndex == 2;
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeSpan.TryParse(TimeBox.Text, out var time))
        {
            MessageBox.Show(this, "Введите корректное время (ЧЧ:ММ)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedDays = new System.Collections.ObjectModel.ObservableCollection<DayOfWeek>();
        if (RepeatCombo.SelectedIndex == 2)
        {
            if (MonCb.IsChecked == true) selectedDays.Add(DayOfWeek.Monday);
            if (TueCb.IsChecked == true) selectedDays.Add(DayOfWeek.Tuesday);
            if (WedCb.IsChecked == true) selectedDays.Add(DayOfWeek.Wednesday);
            if (ThuCb.IsChecked == true) selectedDays.Add(DayOfWeek.Thursday);
            if (FriCb.IsChecked == true) selectedDays.Add(DayOfWeek.Friday);
            if (SatCb.IsChecked == true) selectedDays.Add(DayOfWeek.Saturday);
            if (SunCb.IsChecked == true) selectedDays.Add(DayOfWeek.Sunday);

            if (selectedDays.Count == 0)
            {
                MessageBox.Show(this, "Выберите хотя бы один день недели", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        Result = new ScheduleEntry
        {
            Type = TypeCombo.SelectedIndex == 0 ? ScheduleType.Sleep : ScheduleType.Wake,
            Time = time,
            Repeat = (RepeatType)RepeatCombo.SelectedIndex,
            Enabled = EnabledCb.IsChecked == true,
            SelectedDays = selectedDays
        };

        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
