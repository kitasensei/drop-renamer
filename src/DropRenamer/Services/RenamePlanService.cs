using DropRenamer.Models;

namespace DropRenamer.Services;

public sealed class RenamePlanService
{
    public void BuildPlan(
        IReadOnlyList<FileRenameItem> items,
        FileOperationMode operationMode,
        string? selectedDestination)
    {
        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nextNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var targetFolder = operationMode == FileOperationMode.RenameInPlace
                ? Path.GetDirectoryName(item.OriginalPath)
                : selectedDestination;

            if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
            {
                item.NewName = string.Empty;
                item.DestinationPath = string.Empty;
                item.Status = "送り先を選択してください";
                continue;
            }

            if (operationMode == FileOperationMode.Copy &&
                AreSameDirectory(Path.GetDirectoryName(item.OriginalPath), targetFolder))
            {
                item.NewName = string.Empty;
                item.DestinationPath = string.Empty;
                item.Status = "対象外：コピー元と送り先が同じです";
                continue;
            }

            var folderName = new DirectoryInfo(targetFolder).Name;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                folderName = new DirectoryInfo(targetFolder).Root.Name.TrimEnd(Path.DirectorySeparatorChar);
            }

            var extension = Path.GetExtension(item.OriginalPath);
            var nextNumber = nextNumbers.GetValueOrDefault(targetFolder, 1);
            string destinationPath;
            string newName;

            do
            {
                newName = $"{folderName}_{nextNumber:000}{extension}";
                destinationPath = Path.Combine(targetFolder, newName);
                nextNumber++;
            }
            while (File.Exists(destinationPath) || reservedPaths.Contains(destinationPath));

            nextNumbers[targetFolder] = nextNumber;
            reservedPaths.Add(destinationPath);
            item.NewName = newName;
            item.DestinationPath = destinationPath;
            item.Status = "実行待ち";
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
