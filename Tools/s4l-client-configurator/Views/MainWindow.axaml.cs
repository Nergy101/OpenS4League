using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using S4LClientConfigurator.Services;
using S4LClientConfigurator.ViewModels;

namespace S4LClientConfigurator.Views;

public partial class MainWindow : Window, IClientUi
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
            vm.Ui = this;
    }

    // ---- IClientUi ----------------------------------------------------------

    public async Task<IReadOnlyList<string>> PickFilesAsync(string title, string? filterName = null, string? extension = null)
    {
        var options = new FilePickerOpenOptions { Title = title, AllowMultiple = false };
        if (!string.IsNullOrEmpty(extension))
        {
            options.FileTypeFilter = new[]
            {
                new FilePickerFileType(filterName ?? "Files") { Patterns = new[] { "*" + extension } }
            };
        }
        var result = await StorageProvider.OpenFilePickerAsync(options);
        return result.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Cast<string>().ToList();
    }

    public async Task<string?> PickFromListAsync(string title, IReadOnlyList<string> options)
    {
        if (options.Count == 0) return null;

        var list = new ListBox { MinHeight = 220, MaxHeight = 420, ItemsSource = options.ToList() };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 16, 0, 0)
        };
        var ok = new Button { Content = "Pick", MinWidth = 80, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var bottom = new DockPanel { LastChildFill = false };
        bottom.Children.Add(buttons);
        DockPanel.SetDock(buttons, Dock.Bottom);

        var dialog = new Window
        {
            Title = title,
            Width = 620,
            Height = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Avalonia.Thickness(20),
                Children = { bottom, list }
            }
        };
        DockPanel.SetDock(bottom, Dock.Bottom);

        string? result = null;
        void ConfirmSelection()
        {
            if (list.SelectedIndex >= 0 && list.SelectedIndex < options.Count)
                result = options[list.SelectedIndex];
        }
        ok.Click += (_, _) => { ConfirmSelection(); dialog.Close(true); };
        cancel.Click += (_, _) => dialog.Close(false);
        list.DoubleTapped += (_, _) => { ConfirmSelection(); dialog.Close(true); };

        await dialog.ShowDialog(this);
        return result;
    }

    public Task ShowMessageAsync(string title, string message) => ShowDialogAsync(title, message, confirm: false);

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
}
