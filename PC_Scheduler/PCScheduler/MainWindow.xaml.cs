using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using PCScheduler.Core;
using PCScheduler.Services;

namespace PCScheduler;

public partial class MainWindow : Window
{
    readonly ObservableCollection<ScheduleEntry> _entries = new();
    readonly string _configPath;

    public MainWindow()
    {
        InitializeComponent();
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCScheduler", "schedules.json");
        ScheduleGrid.ItemsSource = _entries;
        Load();
    }

    void Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var data = JsonSerializer.Deserialize<List<ScheduleEntry>>(json);
                if (data != null)
                    foreach (var e in data) _entries.Add(e);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_configPath, JsonSerializer.Serialize(_entries.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    ScheduleEntry? Selected() => ScheduleGrid.SelectedItem as ScheduleEntry;

    void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var ok = Selected() != null;
        EditBtn.IsEnabled = ok;
        ToggleBtn.IsEnabled = ok;
        DeleteBtn.IsEnabled = ok;
    }

    void OnAdd(object sender, RoutedEventArgs e)
    {
        var dlg = new ScheduleDialog(this);
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            _entries.Add(dlg.Result);
            Save();
        }
    }

    void OnEdit(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel == null) return;
        var idx = _entries.IndexOf(sel);
        var dlg = new ScheduleDialog(this, sel);
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            dlg.Result.Id = sel.Id;
            _entries[idx] = dlg.Result;
            Save();
        }
    }

    void OnToggle(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel == null) return;
        sel.Enabled = !sel.Enabled;
        ScheduleGrid.Items.Refresh();
        Save();
    }

    void OnDelete(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel == null) return;
        if (MessageBox.Show("Удалить выбранное расписание?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _entries.Remove(sel);
            Save();
        }
    }

    async void OnDeleteAll(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Удалить все задачи планировщика?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await Task.Run(() => SchedulerService.DeleteAll());
        _entries.Clear();
        Save();
    }

    async void OnApply(object sender, RoutedEventArgs e)
    {
        ApplyBtn.IsEnabled = false;
        ApplyBtn.Content = "Применение...";
        try
        {
            await Task.Run(() => SchedulerService.ApplyAll(_entries.ToList()));
            MessageBox.Show("Расписание успешно применено!", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось применить расписание:\n{ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ApplyBtn.IsEnabled = true;
            ApplyBtn.Content = "Применить";
        }
    }
}
