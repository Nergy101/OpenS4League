using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S4LClientConfigurator.Services;

namespace S4LClientConfigurator.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ResourceService _service = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private IClientUi? _ui;

    public MainWindowViewModel()
    {
        Screens = new ObservableCollection<ScreenRow>();
        OpenCommand = new AsyncRelayCommand(OpenAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public ObservableCollection<ScreenRow> Screens { get; }
    public IAsyncRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Assigned by the window once DataContext is set; builds the screen rows.</summary>
    public IClientUi? Ui
    {
        get => _ui;
        set
        {
            if (_ui != null) return;
            _ui = value;
            BuildScreens();
        }
    }

    private string _status = "Open resource.s4hd to begin.";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    private string _clientPath = "";
    public string ClientPath { get => _clientPath; private set => SetProperty(ref _clientPath, value); }

    private void BuildScreens()
    {
        string[] names =
        {
            "Loading background",
            "Login background",
            "Channel-select background",
            "Server-select background",
            "Startup movie",
        };

        foreach (var name in names)
        {
            var row = new ScreenRow(_service, _ui!, name, SaveSettings);
            if (_settings.ScreenPaths.TryGetValue(name, out var path) && !string.IsNullOrWhiteSpace(path))
                row.LoadPath(path);
            Screens.Add(row);
        }
    }

    private void SaveSettings()
    {
        _settings.ScreenPaths = Screens.ToDictionary(s => s.DisplayName, s => s.ResourcePath);
        _settings.ClientPath = _service.ClientPath;
        _settings.Save();
    }

    private async Task OpenAsync()
    {
        if (_ui is null) return;

        var picked = await _ui.PickFilesAsync("Select resource.s4hd", "S4 resource archive", "s4hd");
        if (picked.Count == 0) return;

        try
        {
            _service.Open(picked[0]);
            ClientPath = _service.ClientPath ?? "";
            foreach (var row in Screens) row.RefreshPreview();
            SaveSettings();
            Status = $"Opened {_service.Zip!.ZipPath}";
        }
        catch (Exception ex)
        {
            await _ui.ShowMessageAsync("Open failed", ex.Message);
        }
    }

    private async Task SaveAsync()
    {
        if (_ui is null || _service.Zip is null)
        {
            await _ui!.ShowMessageAsync("Nothing to save", "Open a resource.s4hd file first.");
            return;
        }

        _service.Save();
        SaveSettings();
        Status = "Saved resource.s4hd";
        await _ui.ShowMessageAsync("Saved", "resource.s4hd has been saved.");
    }
}
