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
                item.Status = "Select a destination folder.";
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
            item.Status = "Ready";
        }
    }

}
