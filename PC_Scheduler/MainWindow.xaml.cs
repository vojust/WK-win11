using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using PCScheduler.Models;
using PCScheduler.Services;

namespace PCScheduler;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ScheduleEntry> _schedules = new();
    private readonly TaskSchedulerService _scheduler = new();
    private readonly string _configPath;

    public MainWindow()
    {
        InitializeComponent();
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCScheduler",
            "schedules.json");

        DataContext = _schedules;
        LoadSchedules();
    }

    private void LoadSchedules()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var entries = JsonSerializer.Deserialize<List<ScheduleEntry>>(json);
                if (entries != null)
                {
                    _schedules.Clear();
                    foreach (var entry in entries)
                        _schedules.Add(entry);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки конфигурации: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveSchedules()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_schedules.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения конфигурации: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ScheduleDialog(this);
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _schedules.Add(dialog.Result);
            SaveSchedules();
        }
    }

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = ScheduleGrid.SelectedItem as ScheduleEntry;
        if (selected == null) return;

        var dialog = new ScheduleDialog(this, selected);
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            var idx = _schedules.IndexOf(selected);
            _schedules.RemoveAt(idx);

            dialog.Result.Id = selected.Id;
            dialog.Result.TaskName = selected.TaskName;
            _schedules.Insert(idx, dialog.Result);
            ScheduleGrid.SelectedItem = dialog.Result;
            SaveSchedules();
        }
    }

    private async void ToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = ScheduleGrid.SelectedItem as ScheduleEntry;
        if (selected == null) return;

        selected.Enabled = !selected.Enabled;

        if (selected.Enabled)
            await _scheduler.CreateOrUpdateTask(selected);
        else
            await _scheduler.DeleteTask(selected);

        SaveSchedules();
        ScheduleGrid.Items.Refresh();
    }

    private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = ScheduleGrid.SelectedItem as ScheduleEntry;
        if (selected == null) return;

        var result = MessageBox.Show("Удалить выбранное расписание?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await _scheduler.DeleteTask(selected);
            _schedules.Remove(selected);
            SaveSchedules();
        }
    }

    private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        ApplyBtn.IsEnabled = false;
        ApplyBtn.Content = "Применение...";

        try
        {
            await _scheduler.ApplyAll(_schedules);
            await _scheduler.CleanupStaleTasks(_schedules);
            MessageBox.Show("Расписание успешно применено!", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка применения расписания: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ApplyBtn.IsEnabled = true;
            ApplyBtn.Content = "Применить";
        }
    }

    private void ScheduleGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var hasSelection = ScheduleGrid.SelectedItem != null;
        EditBtn.IsEnabled = hasSelection;
        DeleteBtn.IsEnabled = hasSelection;
        EnableBtn.IsEnabled = hasSelection;
    }
}
