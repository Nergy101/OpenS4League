namespace S4LClientConfigurator.Services;

/// <summary>Abstraction over window-hosted pickers/dialogs so view models stay UI-agnostic.</summary>
public interface IClientUi
{
    Task<IReadOnlyList<string>> PickFilesAsync(string title, string? filterName = null, string? extension = null);
    Task<string?> PickFromListAsync(string title, IReadOnlyList<string> options);
    Task ShowMessageAsync(string title, string message);
}
