using Avalonia.Controls;
using Avalonia.Threading;
using mProjectLayout.ViewModels;

namespace mProjectLayout.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.LogUpdated += OnLogUpdated;
        };
    }

    private void OnLogUpdated(object? sender, System.EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var logBox = this.FindControl<TextBox>("LogBox");
            if (logBox is null) return;

            // Scroll the underlying ScrollViewer to the bottom
            logBox.CaretIndex = logBox.Text?.Length ?? 0;
        });
    }
}
