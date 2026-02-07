using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

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

    public IRelayCommand BrowseCommand { get; }
    public IRelayCommand GenerateCommand { get; }

    public MainWindowViewModel()
    {
        BrowseCommand = new RelayCommand(async () => await BrowseAsync());
        GenerateCommand = new RelayCommand(async () => await GenerateAsync());
    }

    private void Log(string message)
    {
        LogText += message + "\n";
    }

    private async Task BrowseAsync()
    {
        var lifetime = App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.MainWindow;

        if (window?.StorageProvider is { } provider)
        {
            var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });

            if (folders.Count > 0)
                SelectedDirectory = folders[0].Path.LocalPath;
        }
    }

    private async Task GenerateAsync()
    {
        LogText = string.Empty;
        Log("Starting generation...");

        // No directory selected
        if (string.IsNullOrWhiteSpace(SelectedDirectory) ||
            SelectedDirectory.StartsWith("Click"))
        {
            StatusText = "No directory selected.";
            Log("Error: No directory selected.");
            return;
        }

        // Structure box empty
        if (string.IsNullOrWhiteSpace(StructureText))
        {
            StatusText = "No structure provided.";
            Log("Error: Structure box is empty.");
            return;
        }

        Log($"Base directory: {SelectedDirectory}");
        Log("Parsing structure...");

        var generator = new Services.SkeletonGenerator();
        var (dirs, files, entries) = generator.Generate(SelectedDirectory, StructureText);

        foreach (var entry in entries)
            Log(entry);

        StatusText = $"Finished. Created {dirs} Directories and {files} Files.";
        Log("Generation complete.");
    }
}