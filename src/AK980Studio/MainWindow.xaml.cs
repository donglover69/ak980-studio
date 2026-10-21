using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AK980Studio.Pages;
using AK980Studio.Services;

namespace AK980Studio;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, UserControl> _pages = new();

    public MainWindow()
    {
        InitializeComponent();
        Logo.Source = AppIcon.CreateImageSource(64);
        Icon = AppIcon.CreateImageSource(32);
        ApplyScale();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this, App.Settings.Theme != "Light");
        App.Keyboard.StatusChanged += () => Dispatcher.Invoke(UpdateStatus);
        Show("screen");
    }

    public void ApplyScale()
    {
        UiScale.ScaleX = UiScale.ScaleY = Math.Clamp(App.Settings.UiScale, 0.8, 1.6);
    }

    private void UpdateStatus()
    {
        var kb = App.Keyboard;
        StatusText.Text = kb.IsBusy ? "Working..." : kb.Status;
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,
            kb.IsBusy ? "Accent" : kb.IsConnected ? "Good" : "Warn");
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && IsInitialized)
            Show(tag);
    }

    private void Show(string tag)
    {
        if (!_pages.TryGetValue(tag, out var page))
        {
            page = tag switch
            {
                "lighting" => new LightingPage(),
                "perkey" => new PerKeyPage(),
                "keyboard" => new KeyboardPage(),
                "settings" => new SettingsPage(),
                _ => new ScreenPage(),
            };
            _pages[tag] = page;
        }
        if (PageHost is not null) PageHost.Content = page;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        App.Settings.Save();
        if (App.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        ((App)Application.Current).ExitApp();
    }
}
