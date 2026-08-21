using CommunityToolkit.Mvvm.ComponentModel;
using S4League.Resource;
using S4LResourceTool.App.Services;

namespace S4LResourceTool.App.ViewModels;

public partial class ResourceRow : ObservableObject
{
    public S4ZipEntry Entry { get; }

    public string Name => Entry.Name;
    public string FullName => Entry.FullName;
    public string ChecksumFile => Path.GetFileName(Entry.FileName);
    public string SizeText => ResourceService.HumanSize(Entry.Length);
    public string TypeText => Path.GetExtension(Entry.Name).TrimStart('.').ToUpperInvariant();

    [ObservableProperty] private string _modifiedText = "";
    [ObservableProperty] private bool _isModified;

    public ResourceRow(S4ZipEntry entry)
    {
        Entry = entry;
        RefreshModified();
    }

    public void RefreshModified()
    {
        try
        {
            var t = File.GetLastWriteTime(Entry.FileName);
            ModifiedText = t.Year > 1980 ? t.ToString("yyyy-MM-dd HH:mm") : "";
        }
        catch { ModifiedText = ""; }

        OnPropertyChanged(nameof(SizeText));
    }
}
