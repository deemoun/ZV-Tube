using Avalonia.Controls;
using Avalonia.Input;
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
}
