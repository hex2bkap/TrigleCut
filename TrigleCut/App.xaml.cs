using Microsoft.UI.Xaml;
using TrigleCut.Services;

namespace TrigleCut;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static SettingsService Settings { get; } = new();
    public static LogService Log { get; } = new();

    public App()
    {
        InitializeComponent();
        Settings.Load();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
