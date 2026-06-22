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
    System.Windows.Forms.NotifyIcon? _tray;

    public MainWindow()
    {
        InitializeComponent();
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCScheduler", "schedules.json");
        ScheduleGrid.ItemsSource = _entries;
        Load();
        InitTray();
        var clock = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        clock.Tick += (_, _) => ScheduleGrid.Items.Refresh();
        clock.Start();
    }

    void InitTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Windows.Forms.Application.ExecutablePath),
            Text = "PCScheduler",
            Visible = true,
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Показать", null, (_, _) => { Show(); WindowState = WindowState.Normal; });
        menu.Items.Add("Применить", null, (_, _) => OnApply(null!, null!));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) =>
        {
            _tray.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; };
    }

    void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText(line + "\n");
            LogBox.ScrollToEnd();
        });
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
            Log($"Загружено {_entries.Count} записей");
        }
        catch (Exception ex)
        {
            Log($"Ошибка загрузки: {ex.Message}");
            System.Windows.MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_configPath, JsonSerializer.Serialize(_entries.ToList(),
                new JsonSerializerOptions { WriteIndented = true }));
            Log("Конфиг сохранён");
        }
        catch (Exception ex)
        {
            Log($"Ошибка сохранения: {ex.Message}");
            System.Windows.MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
            Log($"Добавлено: {dlg.Result.TypeDisplay} в {dlg.Result.TimeFormatted}");
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
            Log($"Изменено: {dlg.Result.TypeDisplay} в {dlg.Result.TimeFormatted}");
        }
    }

    void OnToggle(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel == null) return;
        sel.Enabled = !sel.Enabled;
        ScheduleGrid.Items.Refresh();
        Save();
        Log(sel.Enabled ? "Включено" : "Выключено");
    }

    void OnDelete(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel == null) return;
        if (System.Windows.MessageBox.Show("Удалить выбранное расписание?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _entries.Remove(sel);
            Save();
            Log("Запись удалена");
        }
    }

    async void OnDeleteAll(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("Удалить все задачи планировщика и очистить список?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes) return;

        try
        {
            await Task.Run(() => SchedulerService.DeleteAll());
            _entries.Clear();
            Save();
            await RefreshStatus();
            Log("Все задачи удалены");
        }
        catch (Exception ex)
        {
            Log($"Ошибка удаления: {ex.Message}");
            System.Windows.MessageBox.Show($"Не удалось удалить задачи:\n{ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    async void OnApply(object sender, RoutedEventArgs e)
    {
        ApplyBtn.IsEnabled = false;
        ApplyBtn.Content = "Применение...";
        try
        {
            await Task.Run(() => SchedulerService.ApplyAll(_entries.ToList()));
            Log("Расписание применено");
            await RefreshStatus();
            ShowTrayBalloon("Расписание успешно применено!");
        }
        catch (Exception ex)
        {
            Log($"Ошибка применения: {ex.Message}");
            System.Windows.MessageBox.Show($"Не удалось применить расписание:\n{ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ApplyBtn.IsEnabled = true;
            ApplyBtn.Content = "Применить";
        }
    }

    async void OnTest(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("Через 1 минуту ПК уйдёт в сон, через 5 — проснётся. Продолжить?",
                "Тест", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        TestBtn.IsEnabled = false;
        try
        {
            var sleepTime = DateTime.Now.AddMinutes(1);
            var wakeTime = DateTime.Now.AddMinutes(5);
            await Task.Run(() =>
            {
                SchedulerService.Delete("PCSched_test_sleep");
                SchedulerService.Delete("PCSched_test_wake");
                SchedulerService.ScheduleTestTasks(sleepTime, wakeTime);
            });
            Log($"Тест: сон в {sleepTime:HH:mm}, пробуждение в {wakeTime:HH:mm}");
            await RefreshStatus();
        }
        catch (Exception ex)
        {
            Log($"Ошибка теста: {ex.Message}");
            System.Windows.MessageBox.Show($"Ошибка:\n{ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            TestBtn.IsEnabled = true;
        }
    }

    async void OnRefreshStatus(object sender, RoutedEventArgs e) => await RefreshStatus();

    async Task RefreshStatus()
    {
        StatusRefreshBtn.IsEnabled = false;
        try
        {
            var tasks = await Task.Run(() => SchedulerService.QueryActiveTasks());
            var count = tasks.Count;
            StatusText.Text = $"Задач в планировщике: {count}";
            StatusRefreshBtn.IsEnabled = true;
        }
        catch
        {
            StatusText.Text = "Задач в планировщике: ошибка";
            StatusRefreshBtn.IsEnabled = true;
        }
    }

    void ShowTrayBalloon(string text)
    {
        if (_tray != null)
        {
            _tray.BalloonTipTitle = "PCScheduler";
            _tray.BalloonTipText = text;
            _tray.ShowBalloonTip(3000);
        }
    }

    void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            if (_tray != null)
            {
                _tray.Visible = true;
                _tray.ShowBalloonTip(1000, "PCScheduler",
                    "Приложение свёрнуто в трей. Двойной клик — показать.", ToolTipIcon.None);
            }
        }
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _tray?.Dispose();
    }
}
