using Avalonia.Controls;
using Avalonia.Input;
using System.Diagnostics;
using System.Reflection;
using ZVTube.ViewModels;

namespace ZVTube.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DoubleTappedEvent, OnDoubleTapped, handledEventsToo: true);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.OpenInBrowserCommand.CanExecute(null))
        {
            vm.OpenInBrowserCommand.Execute(null);
        }
    }

    private async void OnAboutClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        var versionField = new TextBox
        {
            Text = $"Version: {version}",
            IsReadOnly = true
        };

        var developerField = new TextBox
        {
            Text = "Developer: Dmitry Yarygin",
            IsReadOnly = true
        };

        var repoUrl = "https://github.com/deemoun/ZV-Tube";
        var repoButton = new Button
        {
            Content = $"Repository: {repoUrl}"
        };
        repoButton.Click += (_, _) => OpenUrl(repoUrl);

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var dialog = new Window
        {
            Title = "About",
            Width = 620,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Spacing = 10,
                Margin = new Avalonia.Thickness(16),
                Children =
                {
                    versionField,
                    developerField,
                    repoButton,
                    closeButton
                }
            }
        };

        closeButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
