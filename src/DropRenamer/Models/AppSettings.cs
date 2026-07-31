namespace DropRenamer.Models;

public sealed class AppSettings
{
    public string? LastDestinationPath { get; set; }

    public FileOperationMode OperationMode { get; set; } = FileOperationMode.Copy;
}

public enum FileOperationMode
{
    Copy,
    Move
}
