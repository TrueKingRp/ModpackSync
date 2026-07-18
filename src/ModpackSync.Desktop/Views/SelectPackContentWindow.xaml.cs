using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using ModpackSync.Core.Packs;

namespace ModpackSync.Desktop.Views;

public partial class SelectPackContentWindow : Window
{
    private static readonly HashSet<string> RecommendedNames =
        new(
            StringComparer.OrdinalIgnoreCase)
        {
            "mods",
            "config",
            "scripts",
            "resources",
            "resourcepacks",
            "shaderpacks",
            "defaultconfigs",
            "kubejs",
            "patchouli_books",
            "options.txt",
            "servers.dat"
        };

    private readonly string _packDirectory;

    private readonly ObservableCollection<PackContentItem>
        _items = [];

    private bool _isUpdatingSelection;

    public PackContentSelection Selection
    {
        get;
        private set;
    } = new();

    public SelectPackContentWindow(
        string packDirectory)
    {
        InitializeComponent();

        if (string.IsNullOrWhiteSpace(packDirectory))
        {
            throw new ArgumentException(
                "The pack directory cannot be empty.",
                nameof(packDirectory));
        }

        _packDirectory =
            Path.GetFullPath(packDirectory);

        ContentTreeView.ItemsSource =
            _items;

        LoadTopLevelEntries();
        UpdateSelectionSummary();
    }

    private void LoadTopLevelEntries()
    {
        if (!Directory.Exists(_packDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The pack directory could not be found: " +
                $"{_packDirectory}");
        }

        _items.Clear();

        foreach (string directoryPath
                 in Directory
                     .EnumerateDirectories(_packDirectory)
                     .OrderBy(
                         path => Path.GetFileName(path),
                         StringComparer.OrdinalIgnoreCase))
        {
            PackContentItem item =
                CreateDirectoryItem(
                    directoryPath,
                    parent: null,
                    loadChildren: true);

            RegisterItem(item);

            _items.Add(item);
        }

        foreach (string filePath
                 in Directory
                     .EnumerateFiles(_packDirectory)
                     .OrderBy(
                         path => Path.GetFileName(path),
                         StringComparer.OrdinalIgnoreCase))
        {
            PackContentItem item =
                CreateFileItem(
                    filePath,
                    parent: null);

            RegisterItem(item);

            _items.Add(item);
        }
    }

    private PackContentItem CreateDirectoryItem(
        string directoryPath,
        PackContentItem? parent,
        bool loadChildren)
    {
        string relativePath =
            Path.GetRelativePath(
                    _packDirectory,
                    directoryPath)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/');

        GetDirectoryInformation(
            directoryPath,
            out int fileCount,
            out long totalSize);

        bool recommended =
            parent?.IsIncluded == true ||
            RecommendedNames.Contains(
                Path.GetFileName(directoryPath));

        var item =
            new PackContentItem
            {
                Name =
                    Path.GetFileName(directoryPath),

                RelativePath =
                    relativePath,

                EntryType =
                    "Folder",

                FileCount =
                    fileCount,

                Size =
                    totalSize,

                Parent =
                    parent,

                IsIncluded =
                    recommended
            };

        if (loadChildren)
        {
            LoadImmediateChildren(
                item,
                directoryPath);
        }

        return item;
    }

    private PackContentItem CreateFileItem(
        string filePath,
        PackContentItem? parent)
    {
        string relativePath =
            Path.GetRelativePath(
                    _packDirectory,
                    filePath)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/');

        long size =
            GetFileSize(filePath);

        bool recommended =
            parent?.IsIncluded == true ||
            RecommendedNames.Contains(
                Path.GetFileName(filePath));

        return new PackContentItem
        {
            Name =
                Path.GetFileName(filePath),

            RelativePath =
                relativePath,

            EntryType =
                "File",

            FileCount =
                1,

            Size =
                size,

            Parent =
                parent,

            IsIncluded =
                recommended
        };
    }

    private void LoadImmediateChildren(
        PackContentItem parent,
        string directoryPath)
    {
        try
        {
            foreach (string childDirectory
                     in Directory
                         .EnumerateDirectories(directoryPath)
                         .OrderBy(
                             path => Path.GetFileName(path),
                             StringComparer.OrdinalIgnoreCase))
            {
                /*
                 * We display one layer below the pack root.
                 * Child folders are counted recursively but are
                 * not themselves expanded further.
                 */
                PackContentItem child =
                    CreateDirectoryItem(
                        childDirectory,
                        parent,
                        loadChildren: false);

                RegisterItem(child);

                parent.Children.Add(child);
            }

            foreach (string childFile
                     in Directory
                         .EnumerateFiles(directoryPath)
                         .OrderBy(
                             path => Path.GetFileName(path),
                             StringComparer.OrdinalIgnoreCase))
            {
                PackContentItem child =
                    CreateFileItem(
                        childFile,
                        parent);

                RegisterItem(child);

                parent.Children.Add(child);
            }
        }
        catch
        {
            /*
             * Keep the parent visible even when one of its
             * directories cannot be enumerated.
             */
        }
    }

    private void RegisterItem(
        PackContentItem item)
    {
        item.PropertyChanged +=
            Item_PropertyChanged;
    }

    private static void GetDirectoryInformation(
        string directoryPath,
        out int fileCount,
        out long totalSize)
    {
        fileCount = 0;
        totalSize = 0;

        try
        {
            foreach (string filePath
                     in Directory.EnumerateFiles(
                         directoryPath,
                         "*",
                         SearchOption.AllDirectories))
            {
                fileCount++;

                totalSize +=
                    GetFileSize(filePath);
            }
        }
        catch
        {
            /*
             * Leave the values at whatever could be calculated
             * before the directory became inaccessible.
             */
        }
    }

    private static long GetFileSize(
        string filePath)
    {
        try
        {
            return new FileInfo(
                filePath).Length;
        }
        catch
        {
            return 0;
        }
    }

    private void Item_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName !=
            nameof(PackContentItem.IsIncluded))
        {
            return;
        }

        if (sender is not PackContentItem item)
        {
            return;
        }

        if (_isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;

            /*
             * Changing a folder changes all of its displayed
             * immediate children.
             */
            if (item.Children.Count > 0 &&
                item.IsIncluded.HasValue)
            {
                foreach (PackContentItem child
                         in item.Children)
                {
                    child.IsIncluded =
                        item.IsIncluded.Value;
                }
            }

            /*
             * Changing a child recalculates the parent's
             * checked, unchecked or mixed state.
             */
            if (item.Parent is not null)
            {
                UpdateParentState(
                    item.Parent);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        UpdateSelectionSummary();
    }

    private static void UpdateParentState(
        PackContentItem parent)
    {
        if (parent.Children.Count == 0)
        {
            return;
        }

        bool allIncluded =
            parent.Children.All(
                child =>
                    child.IsIncluded == true);

        bool noneIncluded =
            parent.Children.All(
                child =>
                    child.IsIncluded == false);

        if (allIncluded)
        {
            parent.IsIncluded =
                true;
        }
        else if (noneIncluded)
        {
            parent.IsIncluded =
                false;
        }
        else
        {
            parent.IsIncluded =
                null;
        }
    }

    private void SelectRecommendedButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            _isUpdatingSelection = true;

            foreach (PackContentItem item
                     in _items)
            {
                bool shouldInclude =
                    RecommendedNames.Contains(
                        item.Name);

                item.IsIncluded =
                    shouldInclude;

                foreach (PackContentItem child
                         in item.Children)
                {
                    child.IsIncluded =
                        shouldInclude;
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        UpdateSelectionSummary();
    }

    private void SelectAllButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetAllItemsIncluded(true);
    }

    private void SelectNoneButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetAllItemsIncluded(false);
    }

    private void SetAllItemsIncluded(
        bool included)
    {
        try
        {
            _isUpdatingSelection = true;

            foreach (PackContentItem item
                     in _items)
            {
                item.IsIncluded =
                    included;

                foreach (PackContentItem child
                         in item.Children)
                {
                    child.IsIncluded =
                        included;
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        UpdateSelectionSummary();
    }

    private void ContinueButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var includedPaths =
            new List<string>();

        var excludedPaths =
            new List<string>();

        foreach (PackContentItem item
                 in _items)
        {
            if (item.EntryType == "File")
            {
                if (item.IsIncluded == true)
                {
                    includedPaths.Add(
                        item.RelativePath);
                }

                continue;
            }

            if (item.Children.Count == 0)
            {
                if (item.IsIncluded == true)
                {
                    includedPaths.Add(
                        item.RelativePath);
                }

                continue;
            }

            if (item.IsIncluded == true)
            {
                /*
                 * The complete folder is selected.
                 */
                includedPaths.Add(
                    item.RelativePath);

                continue;
            }

            if (item.IsIncluded is null)
            {
                /*
                 * The parent is partially selected. Include the
                 * parent and explicitly exclude unchecked children.
                 */
                includedPaths.Add(
                    item.RelativePath);

                foreach (PackContentItem child
                         in item.Children)
                {
                    if (child.IsIncluded != true)
                    {
                        excludedPaths.Add(
                            child.RelativePath);
                    }
                }
            }
        }

        includedPaths =
            includedPaths
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        excludedPaths =
            excludedPaths
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (includedPaths.Count == 0)
        {
            MessageBox.Show(
                "Select at least one file or folder.",
                "ModpackSync",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        int selectedFileCount =
            GetSelectedFileCount();

        if (selectedFileCount > 20_000)
        {
            MessageBoxResult result =
                MessageBox.Show(
                    $"This selection contains approximately " +
                    $"{selectedFileCount:N0} files." +
                    $"{Environment.NewLine}{Environment.NewLine}" +
                    "The current server limit is 20,000 files per version, " +
                    "so publishing this version is likely to fail." +
                    $"{Environment.NewLine}{Environment.NewLine}" +
                    "Create the version anyway?",
                    "File Limit Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        Selection =
            new PackContentSelection
            {
                IncludedPaths =
                    includedPaths,

                ExcludedPaths =
                    excludedPaths
            };

        DialogResult =
            true;
    }

    private int GetSelectedFileCount()
    {
        int total =
            0;

        foreach (PackContentItem item
                 in _items)
        {
            if (item.EntryType == "File")
            {
                if (item.IsIncluded == true)
                {
                    total++;
                }

                continue;
            }

            if (item.Children.Count == 0)
            {
                if (item.IsIncluded == true)
                {
                    total +=
                        item.FileCount;
                }

                continue;
            }

            foreach (PackContentItem child
                     in item.Children)
            {
                if (child.IsIncluded == true)
                {
                    total +=
                        child.FileCount;
                }
            }
        }

        return total;
    }

    private long GetSelectedSize()
    {
        long total =
            0;

        foreach (PackContentItem item
                 in _items)
        {
            if (item.EntryType == "File")
            {
                if (item.IsIncluded == true)
                {
                    total +=
                        item.Size;
                }

                continue;
            }

            if (item.Children.Count == 0)
            {
                if (item.IsIncluded == true)
                {
                    total +=
                        item.Size;
                }

                continue;
            }

            foreach (PackContentItem child
                     in item.Children)
            {
                if (child.IsIncluded == true)
                {
                    total +=
                        child.Size;
                }
            }
        }

        return total;
    }

    private void UpdateSelectionSummary()
    {
        int selectedFiles =
            GetSelectedFileCount();

        long selectedSize =
            GetSelectedSize();

        SelectionSummaryTextBlock.Text =
            $"{selectedFiles:N0} files selected, " +
            $"{FormatFileSize(selectedSize)}";
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult =
            false;
    }

    private static string FormatFileSize(
        long byteCount)
    {
        string[] units =
        [
            "B",
            "KB",
            "MB",
            "GB",
            "TB"
        ];

        double size =
            byteCount;

        int unit =
            0;

        while (size >= 1024 &&
               unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}

public sealed class PackContentItem :
    INotifyPropertyChanged
{
    private bool? _isIncluded;

    public bool? IsIncluded
    {
        get =>
            _isIncluded;

        set
        {
            if (_isIncluded == value)
            {
                return;
            }

            _isIncluded =
                value;

            OnPropertyChanged();
        }
    }

    public string Name
    {
        get;
        init;
    } = string.Empty;

    public string RelativePath
    {
        get;
        init;
    } = string.Empty;

    public string EntryType
    {
        get;
        init;
    } = string.Empty;

    public int FileCount
    {
        get;
        init;
    }

    public long Size
    {
        get;
        init;
    }

    public string FormattedSize =>
        SelectPackContentWindowFormatHelper
            .FormatFileSize(Size);

    public bool CanHaveChildren =>
        Children.Count > 0;

    public PackContentItem? Parent
    {
        get;
        init;
    }

    public ObservableCollection<PackContentItem>
        Children
    {
        get;
    } = [];

    public event PropertyChangedEventHandler?
        PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}

internal static class SelectPackContentWindowFormatHelper
{
    public static string FormatFileSize(
        long byteCount)
    {
        string[] units =
        [
            "B",
            "KB",
            "MB",
            "GB",
            "TB"
        ];

        double size =
            byteCount;

        int unit =
            0;

        while (size >= 1024 &&
               unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}