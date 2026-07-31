using System.Collections.ObjectModel;

namespace DropRenamer.ViewModels;

public sealed class FolderNode
{
    private bool _childrenLoaded;

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
}

