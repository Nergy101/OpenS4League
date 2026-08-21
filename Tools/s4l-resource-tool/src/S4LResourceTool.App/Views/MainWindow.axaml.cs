using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using S4LResourceTool.App.Services;
using S4LResourceTool.App.ViewModels;

namespace S4LResourceTool.App.Views;

public partial class MainWindow : Window, IUiServices
{
    public MainWindow()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(FilesGrid, true);
        FilesGrid.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        FilesGrid.AddHandler(DragDrop.DropEvent, OnDrop);
        FilesGrid.SelectionChanged += OnFilesSelectionChanged;

        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                await vm.InitializeAsync();
        };
    }


    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
            vm.Ui = this;
    }

    private void OnFilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.SelectedRows.Clear();
        foreach (var item in FilesGrid.SelectedItems)
            if (item is ResourceRow row)
                vm.SelectedRows.Add(row);
    }

    private void OnFilesDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.OpenExternallyCommand.CanExecute(null))
            vm.OpenExternallyCommand.Execute(null);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles();
        if (files is null) return;

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();

        if (paths.Count > 0)
            await vm.AddFilesAsync(paths);
    }

    // ---- IUiServices --------------------------------------------------------

    public async Task<string?> PickFolderAsync(string title)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<IReadOnlyList<string>> PickFilesAsync(string title, string? extensionFilterName = null, string? extension = null)
    {
        var options = new FilePickerOpenOptions { Title = title, AllowMultiple = false };
        if (!string.IsNullOrEmpty(extension))
        {
            options.FileTypeFilter = new[]
            {
                new FilePickerFileType(extensionFilterName ?? "Files")
                {
                    Patterns = new[] { "*" + extension }
                }
            };
        }

        var result = await StorageProvider.OpenFilePickerAsync(options);
        return result.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Cast<string>().ToList();
    }

    public async Task<string?> PickSaveFileAsync(string suggestedName, string? extension = null)
    {
        var options = new FilePickerSaveOptions { SuggestedFileName = suggestedName };
        if (!string.IsNullOrEmpty(extension))
            options.DefaultExtension = extension.TrimStart('.');

        var file = await StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveImageAsync(string suggestedName)
    {
        var options = new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = "png",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("DDS texture") { Patterns = new[] { "*.dds" } },
            },
        };

        var file = await StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    public Task ShowMessageAsync(string title, string message) => ShowDialogAsync(title, message, confirm: false);

    public async Task<bool> ConfirmAsync(string title, string message) => await ShowDialogAsync(title, message, confirm: true);

    private async Task<bool> ShowDialogAsync(string title, string message, bool confirm)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 16, 0, 0)
        };

        var primary = new Button { Content = confirm ? "Yes" : "OK", MinWidth = 80, IsDefault = true };
        buttons.Children.Add(primary);

        Button? secondary = null;
        if (confirm)
        {
            secondary = new Button { Content = "No", MinWidth = 80, IsCancel = true };
            buttons.Children.Add(secondary);
        }

        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.Height,
            Width = 440,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    buttons
                }
            }
        };

        primary.Click += (_, _) => dialog.Close(true);
        if (secondary is not null)
            secondary.Click += (_, _) => dialog.Close(false);

        return await dialog.ShowDialog<bool>(this);
    }

    public async Task<string?> PickFromListAsync(string title, IReadOnlyList<(string Label, string Value)> options)
    {
        if (options.Count == 0) return null;

        var list = new ListBox
        {
            MinHeight = 220,
            MaxHeight = 420,
            ItemsSource = options.Select(o => o.Label).ToList(),
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 16, 0, 0)
        };
        var ok = new Button { Content = "Open", MinWidth = 80, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var bottom = new DockPanel { LastChildFill = false };
        bottom.Children.Add(buttons);
        DockPanel.SetDock(buttons, Dock.Bottom);

        var dialog = new Window
        {
            Title = title,
            Width = 560,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    bottom,
                    list
                }
            }
        };
        DockPanel.SetDock(bottom, Dock.Bottom);

        string? result = null;
        void ConfirmSelection()
        {
            if (list.SelectedIndex >= 0 && list.SelectedIndex < options.Count)
                result = options[list.SelectedIndex].Value;
        }
        ok.Click += (_, _) => { ConfirmSelection(); dialog.Close(true); };
        cancel.Click += (_, _) => dialog.Close(false);
        list.DoubleTapped += (_, _) => { ConfirmSelection(); dialog.Close(true); };

        await dialog.ShowDialog(this);
        return result;
    }
}
