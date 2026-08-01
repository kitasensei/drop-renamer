namespace DropRenamer.Models;

public sealed class AppSettings
{
    public string? LastDestinationPath { get; set; }

    public FileOperationMode OperationMode { get; set; } = FileOperationMode.Copy;

    // Window and detail-grid layout settings added after the initial .NET 10 version.
    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public bool IsWindowMaximized { get; set; }

    public List<DetailColumnSettings> DetailColumns { get; set; } = [];
}

public sealed class DetailColumnSettings
{
    public string Key { get; set; } = string.Empty;

    public double Width { get; set; }

    public int DisplayIndex { get; set; }
}

public enum FileOperationMode
{
    Copy,
    Move
}
