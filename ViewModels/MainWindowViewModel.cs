using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace mProjectLayout.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string selectedDirectory = "Click Browse to Select Project Directory";

    [ObservableProperty]
    private string structureText = string.Empty;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string logText = string.Empty;

    public event EventHandler? LogUpdated;

    private bool _hasGeneratedInCurrentDirectory = false;
    private string _lastGeneratedDirectory = string.Empty;

    public IRelayCommand BrowseCommand   { get; }
    public IRelayCommand GenerateCommand { get; }
    public IRelayCommand ClearCommand    { get; }
    public ICommand      ShowHelpCommand { get; }

    public MainWindowViewModel()
    {
        BrowseCommand   = new RelayCommand(async () => await BrowseAsync());
        GenerateCommand = new RelayCommand(async () => await GenerateAsync());
        ClearCommand    = new RelayCommand(ClearStructure);
        ShowHelpCommand = new RelayCommand(ShowHelp);
    }

    private void Log(string message)
    {
        LogText += message + "\n";
        LogUpdated?.Invoke(this, EventArgs.Empty);
    }

    private async Task BrowseAsync()
    {
        var lifetime = App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window   = lifetime?.MainWindow;

        if (window?.StorageProvider is { } provider)
        {
            var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                SelectedDirectory = folders[0].Path.LocalPath;
                _hasGeneratedInCurrentDirectory = false;
            }
        }
    }

    private async Task GenerateAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedDirectory) ||
                SelectedDirectory.StartsWith("Click"))
            {
                StatusText = "❌ No directory selected.";
                Log("❌ Error: No directory selected.");
                return;
            }

            if (string.IsNullOrWhiteSpace(StructureText))
            {
                StatusText = "❌ No structure provided.";
                Log("❌ Error: Structure box is empty.");
                return;
            }

            // Warn if generating into the same directory a second time
            // Uses a simple dialog built from Avalonia native controls
            if (_hasGeneratedInCurrentDirectory &&
                _lastGeneratedDirectory == SelectedDirectory)
            {
                var confirmed = await ShowConfirmDialogAsync(
                    "Generate Again?",
                    $"You already generated a layout into:\n\n{SelectedDirectory}\n\nExisting files will be skipped. Continue?");

                if (!confirmed)
                {
                    Log("⚠  Generation cancelled by user.");
                    StatusText = "Cancelled.";
                    return;
                }
            }

            LogText = string.Empty;
            Log("🚀 Starting generation...");
            Log($"📂 Base directory: {SelectedDirectory}");
            Log("🔎 Parsing structure...");

            var generator = new Services.SkeletonGenerator();
            var (dirs, files, entries) = generator.Generate(SelectedDirectory, StructureText);

            foreach (var entry in entries)
                Log(entry);

            StatusText = $"✅ Finished. Created {dirs} Directories and {files} Files.";
            Log($"✅ Generation complete. {dirs} dirs, {files} files.");

            _hasGeneratedInCurrentDirectory = true;
            _lastGeneratedDirectory = SelectedDirectory;
        }
        catch (Exception ex)
        {
            StatusText = "❌ Error during generation.";
            Log($"❌ Exception: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                Log($"   Inner: {ex.InnerException.Message}");
        }
    }

    // Simple Yes/No dialog using native Avalonia — no extra package needed
    private static async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var lifetime = App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var parent   = lifetime?.MainWindow;
        if (parent == null) return true; // no window, just proceed

        var dialog = new Window
        {
            Title           = title,
            Width           = 420,
            Height          = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize       = false,
        };

        bool result = false;

        var msgBlock = new TextBlock
        {
            Text       = message,
            Margin     = new Avalonia.Thickness(20, 20, 20, 12),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var yesBtn = new Button { Content = "Yes", Width = 80, Margin = new Avalonia.Thickness(0, 0, 8, 0) };
        var noBtn  = new Button { Content = "No",  Width = 80 };

        yesBtn.Click += (_, _) => { result = true;  dialog.Close(); };
        noBtn.Click  += (_, _) => { result = false; dialog.Close(); };

        var btnRow = new Avalonia.Controls.StackPanel
        {
            Orientation         = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin              = new Avalonia.Thickness(20, 0, 20, 20),
        };
        btnRow.Children.Add(yesBtn);
        btnRow.Children.Add(noBtn);

        var layout = new Avalonia.Controls.StackPanel();
        layout.Children.Add(msgBlock);
        layout.Children.Add(btnRow);

        dialog.Content = layout;

        await dialog.ShowDialog(parent);
        return result;
    }

    private void ClearStructure()
    {
        StructureText = string.Empty;
        StatusText    = "Ready";
        LogText       = string.Empty;
        _hasGeneratedInCurrentDirectory = false;
        Log("🧹 Cleared.");
    }

    private void ShowHelp()
    {
        Log("── mProjectLayout Help ──────────────────────────────");
        Log("1. Browse   → select your base directory.");
        Log("2. Paste    → drop any supported layout into the box.");
        Log("3. Generate → creates folders and empty skeleton files.");
        Log("");
        Log("Supported input formats:");
        Log("  🌳 Tree     (├── └── │  box-drawing — AI output)");
        Log("  📂 Paths    (dir\\subdir\\file.cs)");
        Log("  ↔  Indented (spaces only, any indent width)");
        Log("");
        Log("Clear resets the input box, log, and status.");
        Log("────────────────────────────────────────────────────");
    }
}
