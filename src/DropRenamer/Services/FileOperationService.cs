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
                progress.Report((item, "Destination folder is not set."));
                continue;
            }

            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(item.DestinationPath))
                    {
                        throw new IOException("A file with the same name already exists."));
                    }

                    if (operationMode == FileOperationMode.Copy)
                    {
                        File.Copy(item.OriginalPath, item.DestinationPath, overwrite: false);
                    }
                    else
                    {
                        File.Move(item.OriginalPath, item.DestinationPath);
                    }
                }, cancellationToken);

                progress.Report((item, "Completed"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                progress.Report((item, $"Error: {ex.Message}"));
            }
        }
    }
}
