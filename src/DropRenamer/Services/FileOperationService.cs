using DropRenamer.Models;

namespace DropRenamer.Services;

public sealed class FileOperationService
{
    public async Task ExecuteAsync(
        IEnumerable<FileRenameItem> items,
        FileOperationMode operationMode,
        IProgress<(FileRenameItem Item, string Status)> progress,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(item.DestinationPath))
            {
                progress.Report((item, "送り先が未設定です"));
                continue;
            }

            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(item.DestinationPath))
                    {
                        throw new IOException("同名のファイルがすでに存在します。");
                    }

                    var sourceFolder = Path.GetDirectoryName(item.OriginalPath);
                    var destinationFolder = Path.GetDirectoryName(item.DestinationPath);
                    var renameInPlace = AreSameDirectory(sourceFolder, destinationFolder);

                    if (operationMode == FileOperationMode.Copy && !renameInPlace)
                    {
                        File.Copy(item.OriginalPath, item.DestinationPath, overwrite: false);
                    }
                    else
                    {
                        File.Move(item.OriginalPath, item.DestinationPath);
                    }
                }, cancellationToken);

                progress.Report((item, "完了"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                progress.Report((item, $"エラー: {ex.Message}"));
            }
        }
    }

    private static bool AreSameDirectory(string? firstPath, string? secondPath)
    {
        if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
        {
            return false;
        }

        var normalizedFirst = Path.TrimEndingDirectorySeparator(Path.GetFullPath(firstPath));
        var normalizedSecond = Path.TrimEndingDirectorySeparator(Path.GetFullPath(secondPath));

        return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
    }
}
