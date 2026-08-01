// Changes from the initial .NET 10 version:
// - Show "Rename (same folder)" when the actual operation is an in-place rename.
// - Display that operation in orange so it is distinct from red warnings.
// - Show all four detail headers in the default layout.
// - Save and restore the window size, position, state, and detail-column layout.
// - Reset an off-screen saved window position to the default centered position.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DropRenamer.Models;
using DropRenamer.Services;
using DropRenamer.ViewModels;

namespace DropRenamer;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly RenamePlanService _renamePlanService = new();
    private readonly FileOperationService _fileOperationService = new();
    private readonly AppSettings _settings;
    private bool _isExecuting;
    private ListSortDirection _originalNameSortDirection = ListSortDirection.Ascending;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _settings = _settingsService.Load();
        LoadRootFolders();
        RestoreSettings();
        RefreshPlan();
        OriginalNameColumn.SortDirection = _originalNameSortDirection;
    }

    public ObservableCollection<FolderNode> RootFolders { get; } = [];

    public ObservableCollection<FileRenameItem> FileItems { get; } = [];

    private FileOperationMode CurrentOperation =>
        MoveRadio.IsChecked == true
            ? FileOperationMode.Move
            : FileOperationMode.Copy;

    private string? SelectedDestination { get; set; }

    private void LoadRootFolders()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady))
        {
            RootFolders.Add(new FolderNode(drive.RootDirectory.FullName, drive.Name));
        }
    }

    private void RestoreSettings()
    {
        RestoreWindowLayout();
        RestoreDetailColumnLayout();

        SelectedDestination = Directory.Exists(_settings.LastDestinationPath)
            ? _settings.LastDestinationPath
            : null;

        switch (_settings.OperationMode)
        {
            case FileOperationMode.Move:
                MoveRadio.IsChecked = true;
                break;
            default:
                CopyRadio.IsChecked = true;
                break;
        }

        UpdateDestinationDisplay();
        RestoreFolderTreeSelection();
    }

    private void RestoreWindowLayout()
    {
        if (!_settings.WindowLeft.HasValue
            || !_settings.WindowTop.HasValue
            || !_settings.WindowWidth.HasValue
            || !_settings.WindowHeight.HasValue)
        {
            return;
        }

        var width = _settings.WindowWidth.Value;
        var height = _settings.WindowHeight.Value;
        var left = _settings.WindowLeft.Value;
        var top = _settings.WindowTop.Value;

        if (!IsValidDimension(width, MinWidth)
            || !IsValidDimension(height, MinHeight)
            || !IsSavedWindowVisible(left, top, width, height))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    private void RestoreDetailColumnLayout()
    {
        var columnsByKey = GetDetailColumns().ToDictionary(entry => entry.Key, entry => entry.Column);
        var validSettings = (_settings.DetailColumns ?? [])
            .Where(setting => columnsByKey.ContainsKey(setting.Key)
                              && double.IsFinite(setting.Width)
                              && setting.Width >= 40
                              && setting.DisplayIndex >= 0
                              && setting.DisplayIndex < columnsByKey.Count)
            .GroupBy(setting => setting.Key)
            .Select(group => group.First())
            .ToList();

        if (validSettings.Count != columnsByKey.Count
            || validSettings.Select(setting => setting.DisplayIndex).Distinct().Count()
                != columnsByKey.Count)
        {
            return;
        }

        foreach (var setting in validSettings.OrderBy(setting => setting.DisplayIndex))
        {
            var column = columnsByKey[setting.Key];
            column.Width = new DataGridLength(setting.Width);
            column.DisplayIndex = setting.DisplayIndex;
        }
    }

    private static bool IsValidDimension(double value, double minimum) =>
        double.IsFinite(value) && value >= minimum;

    private static bool IsSavedWindowVisible(double left, double top, double width, double height)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return false;
        }

        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        var intersectionWidth = Math.Min(left + width, virtualRight) - Math.Max(left, virtualLeft);
        var intersectionHeight = Math.Min(top + height, virtualBottom) - Math.Max(top, virtualTop);

        return intersectionWidth >= Math.Min(120, width)
               && intersectionHeight >= Math.Min(80, height);
    }

    private IEnumerable<(string Key, DataGridColumn Column)> GetDetailColumns()
    {
        yield return ("OriginalName", OriginalNameColumn);
        yield return ("NewName", NewNameColumn);
        yield return ("Destination", DestinationColumn);
        yield return ("Status", StatusColumn);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_settings.IsWindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveLayoutSettings();
        SaveSettings();
    }

    private void SaveLayoutSettings()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;

        if (IsValidDimension(bounds.Width, MinWidth)
            && IsValidDimension(bounds.Height, MinHeight))
        {
            _settings.WindowLeft = bounds.Left;
            _settings.WindowTop = bounds.Top;
            _settings.WindowWidth = bounds.Width;
            _settings.WindowHeight = bounds.Height;
        }

        _settings.IsWindowMaximized = WindowState == WindowState.Maximized;
        _settings.DetailColumns = GetDetailColumns()
            .Select(entry => new DetailColumnSettings
            {
                Key = entry.Key,
                Width = entry.Column.ActualWidth,
                DisplayIndex = entry.Column.DisplayIndex
            })
            .ToList();
    }

    private void RestoreFolderTreeSelection()
    {
        if (string.IsNullOrWhiteSpace(SelectedDestination))
        {
            return;
        }

        var currentNode = RootFolders.FirstOrDefault(
            node => IsSameOrAncestor(node.FullPath, SelectedDestination));
        if (currentNode is null)
        {
            return;
        }

        while (!PathsEqual(currentNode.FullPath, SelectedDestination))
        {
            currentNode.LoadChildren();
            currentNode.IsExpanded = true;

            var nextNode = currentNode.Children.FirstOrDefault(
                node => !node.IsPlaceholder
                        && IsSameOrAncestor(node.FullPath, SelectedDestination));
            if (nextNode is null)
            {
                return;
            }

            currentNode = nextNode;
        }

        currentNode.IsSelected = true;
        var selectedNode = currentNode;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => BringFolderIntoView(selectedNode)));
    }

    private void BringFolderIntoView(FolderNode selectedNode)
    {
        FolderTree.UpdateLayout();

        if (FindTreeViewItem(FolderTree, selectedNode) is { } container)
        {
            container.BringIntoView();
        }
    }

    private static TreeViewItem? FindTreeViewItem(
        ItemsControl parent,
        FolderNode selectedNode)
    {
        foreach (var item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item)
                is not TreeViewItem container)
            {
                continue;
            }

            if (ReferenceEquals(container.DataContext, selectedNode))
            {
                return container;
            }

            if (container.IsExpanded
                && FindTreeViewItem(container, selectedNode) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool IsSameOrAncestor(string ancestorPath, string targetPath)
    {
        try
        {
            var relativePath = Path.GetRelativePath(ancestorPath, targetPath);
            return relativePath == "."
                   || (!Path.IsPathRooted(relativePath)
                       && relativePath != ".."
                       && !relativePath.StartsWith(
                           $"..{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool PathsEqual(string leftPath, string rightPath)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(leftPath),
            Path.TrimEndingDirectorySeparator(rightPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = ContainsFiles(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var droppedPaths = (string[])e.Data.GetData(DataFormats.FileDrop);
        var existingPaths = FileItems
            .Select(item => item.OriginalPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in droppedPaths.Where(File.Exists))
        {
            if (existingPaths.Add(path))
            {
                FileItems.Add(new FileRenameItem(path));
            }
        }

        SortFileItems();
        RefreshPlan();
    }

    private static bool ContainsFiles(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        return ((string[])data.GetData(DataFormats.FileDrop)).Any(File.Exists);
    }

    private void FolderItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: FolderNode node })
        {
            node.LoadChildren();
        }
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not FolderNode { IsPlaceholder: false } node)
        {
            return;
        }

        SelectedDestination = node.FullPath;
        SaveSettings();
        UpdateDestinationDisplay();
        RefreshPlan();
    }

    private void OperationRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || _settings is null)
        {
            return;
        }

        SaveSettings();
        UpdateDestinationDisplay();
        RefreshPlan();
    }

    private void UpdateDestinationDisplay()
    {
        DestinationText.Text = SelectedDestination ?? "No destination folder selected";
    }

    private void RefreshPlan()
    {
        _renamePlanService.BuildPlan(FileItems, CurrentOperation, SelectedDestination);

        var readyCount = FileItems.Count(item => !string.IsNullOrWhiteSpace(item.DestinationPath));
        SummaryText.Text = FileItems.Count == 0
            ? "No files have been added yet."
            : $"Files: {FileItems.Count} (ready: {readyCount})";

        ExecuteButton.IsEnabled = !_isExecuting && readyCount > 0;
    }

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        var readyItems = FileItems
            .Where(item => !string.IsNullOrWhiteSpace(item.DestinationPath))
            .ToList();

        if (_isExecuting || readyItems.Count == 0)
        {
            return;
        }

        _isExecuting = true;
        ExecuteButton.IsEnabled = false;
        var progress = new Progress<(FileRenameItem Item, string Status)>(
            update => update.Item.Status = update.Status);

        await _fileOperationService.ExecuteAsync(readyItems, CurrentOperation, progress);

        _isExecuting = false;
        var totalCount = readyItems.Count;
        var completedItems = readyItems
            .Where(item => item.Status == "Completed")
            .ToList();
        var successCount = completedItems.Count;
        var failureCount = totalCount - successCount;

        foreach (var item in completedItems)
        {
            FileItems.Remove(item);
        }

        PreviewGrid.UnselectAll();
        UpdateClearButton();
        ExecuteButton.IsEnabled = failureCount > 0;
        SummaryText.Text = failureCount == 0
            ? $"Completed: {successCount}."
            : $"Completed: {successCount}. Failed: {failureCount}.";
    }

    private void PreviewGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateClearButton();
    }

    private void PreviewGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        _originalNameSortDirection =
            e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        e.Column.SortDirection = _originalNameSortDirection;

        SortFileItems();
        RefreshPlan();
    }

    private void SortFileItems()
    {
        IEnumerable<FileRenameItem> orderedItems =
            _originalNameSortDirection == ListSortDirection.Ascending
                ? FileItems
                    .OrderBy(item => item.OriginalName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(item => item.OriginalPath, StringComparer.OrdinalIgnoreCase)
                : FileItems
                    .OrderByDescending(item => item.OriginalName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenByDescending(item => item.OriginalPath, StringComparer.OrdinalIgnoreCase);

        var sortedItems = orderedItems.ToList();
        for (var targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
        {
            var currentIndex = FileItems.IndexOf(sortedItems[targetIndex]);
            if (currentIndex != targetIndex)
            {
                FileItems.Move(currentIndex, targetIndex);
            }
        }
    }

    private void PreviewGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var clickedRow = ItemsControl.ContainerFromElement(PreviewGrid, source) as DataGridRow;
        if (clickedRow is null)
        {
            PreviewGrid.UnselectAll();
        }
    }

    private void UpdateClearButton()
    {
        ClearButton.Content = PreviewGrid.SelectedItems.Count > 0
            ? "Clear Selected"
            : "Clear Entire List";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isExecuting)
        {
            return;
        }

        var selectedItems = PreviewGrid.SelectedItems
            .Cast<FileRenameItem>()
            .ToList();

        if (selectedItems.Count == 0)
        {
            FileItems.Clear();
        }
        else
        {
            foreach (var item in selectedItems)
            {
                FileItems.Remove(item);
            }
        }

        PreviewGrid.UnselectAll();
        UpdateClearButton();
        RefreshPlan();
    }

    private void SaveSettings()
    {
        _settings.LastDestinationPath = SelectedDestination;
        _settings.OperationMode = CurrentOperation;
        _settingsService.Save(_settings);
    }
}
