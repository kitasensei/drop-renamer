using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DropRenamer.Models;

public sealed class FileRenameItem : INotifyPropertyChanged
{
    private string _newName = string.Empty;
    private string _destinationPath = string.Empty;
    private string _status = "待機中";

    public FileRenameItem(string originalPath)
    {
        OriginalPath = originalPath;
    }

    public string OriginalPath { get; }

    public string OriginalName => Path.GetFileName(OriginalPath);

    public string NewName
    {
        get => _newName;
        set => SetField(ref _newName, value);
    }

    public string DestinationPath
    {
        get => _destinationPath;
        set => SetField(ref _destinationPath, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

