using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _settings = _settingsService.Load();
        LoadRootFolders();
        RestoreSettings();
        RefreshPlan();
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
        DestinationText.Text = SelectedDestination ?? "送り先が選択されていません";
    }

    private void RefreshPlan()
    {
        _renamePlanService.BuildPlan(FileItems, CurrentOperation, SelectedDestination);

        var readyCount = FileItems.Count(item => !string.IsNullOrWhiteSpace(item.DestinationPath));
        SummaryText.Text = FileItems.Count == 0
            ? "ファイルはまだ追加されていません。"
            : $"{FileItems.Count}個のファイル（実行可能: {readyCount}個）";

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
            .Where(item => item.Status == "完了")
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
            ? $"{successCount}個の処理が完了しました。"
            : $"{successCount}個の処理が完了しました。{failureCount}個の処理に失敗しました。";
    }

    private void PreviewGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateClearButton();
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
            ? "選択項目をクリア"
            : "一覧全体をクリア";
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
