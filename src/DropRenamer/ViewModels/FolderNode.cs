using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DropRenamer.ViewModels;

public sealed class FolderNode : INotifyPropertyChanged
{
    private bool _childrenLoaded;
    private bool _isExpanded;
    private bool _isSelected;

    public FolderNode(string fullPath, string? displayName = null)
    {
        FullPath = fullPath;
        DisplayName = displayName ?? GetDisplayName(fullPath);

        if (CanHaveChildren(fullPath))
        {
            Children.Add(CreatePlaceholder());
        }
    }

    private FolderNode()
    {
        FullPath = string.Empty;
        DisplayName = string.Empty;
        IsPlaceholder = true;
    }

    public string FullPath { get; }

    public string DisplayName { get; }

    public bool IsPlaceholder { get; }

    public ObservableCollection<FolderNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void LoadChildren()
    {
        if (_childrenLoaded || IsPlaceholder)
        {
            return;
        }

        _childrenLoaded = true;
        Children.Clear();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(FullPath)
                         .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase))
            {
                try
                {
                    Children.Add(new FolderNode(directory));
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip folders Windows does not allow the current user to inspect.
                }
                catch (IOException)
                {
                    // Skip folders that disappear or become unavailable while loading.
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static FolderNode CreatePlaceholder() => new();

    private static bool CanHaveChildren(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static string GetDisplayName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
