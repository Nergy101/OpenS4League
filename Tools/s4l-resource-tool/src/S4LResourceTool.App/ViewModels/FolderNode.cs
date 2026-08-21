using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace S4LResourceTool.App.ViewModels;

public partial class FolderNode : ObservableObject
{
    public string Name { get; }
    public string FullPath { get; }
    public ObservableCollection<FolderNode> Children { get; } = new();

    [ObservableProperty] private bool _isExpanded;

    public FolderNode(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
    }
}
