namespace S4LResourceTool.App.Services;

/// <summary>Platform/dialog interactions the view model needs; implemented by the main window.</summary>
public interface IUiServices
{
    Task<string?> PickFolderAsync(string title);
    Task<IReadOnlyList<string>> PickFilesAsync(string title, string? extensionFilterName = null, string? extension = null);
    Task<string?> PickSaveFileAsync(string suggestedName, string? extension = null);

    /// <summary>Save dialog offering PNG and DDS (BC7) targets; format inferred from the chosen path's extension.</summary>
    Task<string?> PickSaveImageAsync(string suggestedName);
    Task ShowMessageAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Lets the user choose one option from a list; returns the chosen option's Value, or null if cancelled.</summary>
    Task<string?> PickFromListAsync(string title, IReadOnlyList<(string Label, string Value)> options);
}
