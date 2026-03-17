using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ZVTube.Services;
using ZVTube.ViewModels;
using ZVTube.Views;

namespace ZVTube;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var settingsService = new SettingsService();
        var toolManager = new ToolManager(settingsService);
        var videoService = new VideoService(toolManager, settingsService);
        var localizationService = new LocalizationService(settingsService);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(videoService, settingsService, localizationService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
