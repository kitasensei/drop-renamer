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
            var targetFolder = selectedDestination;

            if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
            {
                item.NewName = string.Empty;
                item.DestinationPath = string.Empty;
                item.Status = "送り先を選択してください";
                continue;
            }

            var folderName = new DirectoryInfo(targetFolder).Name;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                folderName = new DirectoryInfo(targetFolder).Root.Name.TrimEnd(Path.DirectorySeparatorChar);
            }

            var renameInPlace = AreSameDirectory(
                Path.GetDirectoryName(item.OriginalPath),
                targetFolder);

            if (renameInPlace && IsAlreadyRenamed(item.OriginalPath, folderName))
            {
                item.NewName = string.Empty;
                item.DestinationPath = string.Empty;
                item.Status = "リネーム済みです";
                continue;
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
            // Show the operation that will actually run, regardless of the selected mode.
            item.Status = renameInPlace ? "リネーム（同じフォルダー）" : "実行待ち";
        }
    }

    private static bool IsAlreadyRenamed(string filePath, string folderName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var expectedPrefix = $"{folderName}_";

        if (!nameWithoutExtension.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sequence = nameWithoutExtension[expectedPrefix.Length..];
        return sequence.Length >= 3
               && sequence.All(char.IsAsciiDigit)
               && int.TryParse(sequence, out var number)
               && number > 0;
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
