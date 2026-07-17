using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ModpackSync.Contracts.Instances;
using ModpackSync.Contracts.Manifests;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Versions;
using ModpackSync.Core.Instances;
using ModpackSync.Core.Packs;
using ModpackSync.Core.Versions;
using ModpackSync.Desktop.Models;

namespace ModpackSync.Desktop;

public partial class MainWindow : Window
{
    private readonly PackManager _packManager;
    private readonly VersionManager _versionManager;
    private readonly InstanceSettingsManager _instanceSettingsManager;
    private readonly InstanceDiscoveryService _instanceDiscoveryService;

    private readonly ObservableCollection<InstanceListItem> _instances = [];

    private bool _isInitialised;

    public MainWindow()
    {
        InitializeComponent();

        _packManager = new PackManager();
        _versionManager = new VersionManager();
        _instanceSettingsManager = new InstanceSettingsManager();

        _instanceDiscoveryService =
            new InstanceDiscoveryService(_packManager);

        InstancesDataGrid.ItemsSource = _instances;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_isInitialised)
        {
            return;
        }

        _isInitialised = true;

        try
        {
            SetBusyState(
                true,
                "Loading ModpackSync...");

            await _packManager.InitialiseAsync();
            await _versionManager.InitialiseAsync();
            await _instanceSettingsManager.InitialiseAsync();

            InstancesDirectoryTextBox.Text =
                _instanceSettingsManager.Settings.InstancesDirectory
                ?? string.Empty;

            await RefreshInstancesAsync();

            SetBusyState(
                false,
                "Ready.");
        }
        catch (Exception ex)
        {
            SetBusyState(
                false,
                $"Failed to initialise: {ex.Message}");

            MessageBox.Show(
                ex.Message,
                "ModpackSync",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void BrowseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select your Prism Launcher instances folder",
            Multiselect = false
        };

        string? existingDirectory =
            _instanceSettingsManager.Settings.InstancesDirectory;

        if (!string.IsNullOrWhiteSpace(existingDirectory) &&
            Directory.Exists(existingDirectory))
        {
            dialog.InitialDirectory = existingDirectory;
        }

        bool? result = dialog.ShowDialog(this);

        if (result != true)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                "Saving instances folder...");

            await _instanceSettingsManager
                .SetInstancesDirectoryAsync(
                    dialog.FolderName);

            InstancesDirectoryTextBox.Text =
                dialog.FolderName;

            await RefreshInstancesAsync();

            SetBusyState(
                false,
                $"Found {_instances.Count} Prism instances.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The instances folder could not be saved.",
                ex);
        }
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            SetBusyState(
                true,
                "Refreshing instances...");

            await RefreshInstancesAsync();

            SetBusyState(
                false,
                $"Found {_instances.Count} Prism instances.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The instances could not be refreshed.",
                ex);
        }
    }

    private async Task RefreshInstancesAsync()
    {
        _instances.Clear();

        string? instancesDirectory =
            _instanceSettingsManager.Settings.InstancesDirectory;

        if (string.IsNullOrWhiteSpace(instancesDirectory))
        {
            StatusTextBlock.Text =
                "Choose your Prism Launcher instances folder.";

            UpdateActionButtons();
            return;
        }

        IReadOnlyList<LauncherInstance> discoveredInstances =
            _instanceDiscoveryService.Discover(
                instancesDirectory);

        foreach (LauncherInstance instance in discoveredInstances)
        {
            ModpackRegistration? managedPack =
                _packManager.Packs.FirstOrDefault(pack =>
                    PathsAreEqual(
                        pack.LocalPath,
                        instance.MinecraftPath));

            _instances.Add(
                new InstanceListItem
                {
                    Instance = instance,
                    ManagedPack = managedPack
                });
        }

        UpdateActionButtons();

        await Task.CompletedTask;
    }

    private async void ManagePackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        if (selectedItem is null ||
            selectedItem.IsManaged)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                $"Registering {selectedItem.Name}...");

            await _packManager.AddPackAsync(
                selectedItem.Name,
                selectedItem.MinecraftPath);

            await RefreshInstancesAsync();

            SelectInstanceByPath(
                selectedItem.MinecraftPath);

            SetBusyState(
                false,
                $"{selectedItem.Name} is now managed.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The pack could not be registered.",
                ex);
        }
    }

    private async void ScanPackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        if (selectedItem?.ManagedPack is null)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                $"Scanning {selectedItem.Name}...");

            ModpackManifest manifest =
                await _packManager.ScanPackAsync(
                    selectedItem.ManagedPack.Id);

            SetBusyState(
                false,
                $"Scan complete: {manifest.Files.Count} files found.");

            await RefreshInstancesAsync();

            SelectInstanceByPath(
                selectedItem.MinecraftPath);
        }
        catch (Exception ex)
        {
            ShowError(
                "The pack could not be scanned.",
                ex);
        }
    }

    private async void CreateVersionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        if (selectedItem?.ManagedPack is null)
        {
            return;
        }

        string versionLabel =
            Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the new version label:",
                $"Create version — {selectedItem.Name}",
                "1.0.0");

        if (string.IsNullOrWhiteSpace(versionLabel))
        {
            return;
        }

        string releaseNotes =
            Microsoft.VisualBasic.Interaction.InputBox(
                "Enter release notes:",
                $"Release notes — {versionLabel}",
                string.Empty);

        try
        {
            SetBusyState(
                true,
                $"Creating version {versionLabel}...");

            await _packManager.ScanPackAsync(
                selectedItem.ManagedPack.Id);

            PackVersion version =
                await _versionManager.CreateVersionAsync(
                    selectedItem.ManagedPack,
                    versionLabel,
                    releaseNotes);

            SetBusyState(
                false,
                $"Created version {version.VersionLabel}.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The version could not be created.",
                ex);
        }
    }

    private void ViewVersionsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        if (selectedItem?.ManagedPack is null)
        {
            return;
        }

        IReadOnlyList<PackVersion> versions =
            _versionManager.GetVersions(
                selectedItem.ManagedPack.Id);

        if (versions.Count == 0)
        {
            MessageBox.Show(
                "This pack does not have any local versions yet.",
                selectedItem.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        string versionText = string.Join(
            Environment.NewLine + Environment.NewLine,
            versions.Select(version =>
                $"{version.VersionLabel}" +
                (version.IsPublished
                    ? " [Published]"
                    : string.Empty) +
                Environment.NewLine +
                $"Created: {version.CreatedAt.ToLocalTime():dd MMM yyyy HH:mm}" +
                Environment.NewLine +
                $"Notes: {version.ReleaseNotes ?? "None"}"));

        MessageBox.Show(
            versionText,
            $"{selectedItem.Name} versions",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void InstancesDataGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateActionButtons();
    }

    private InstanceListItem? GetSelectedInstance()
    {
        return InstancesDataGrid.SelectedItem
            as InstanceListItem;
    }

    private void UpdateActionButtons()
    {
        InstanceListItem? selectedItem =
            GetSelectedInstance();

        bool hasSelection =
            selectedItem is not null;

        bool isManaged =
            selectedItem?.IsManaged == true;

        ManagePackButton.IsEnabled =
            hasSelection && !isManaged;

        ScanPackButton.IsEnabled =
            isManaged;

        CreateVersionButton.IsEnabled =
            isManaged;

        ViewVersionsButton.IsEnabled =
            isManaged;
    }

    private void SelectInstanceByPath(
        string minecraftPath)
    {
        InstanceListItem? matchingItem =
            _instances.FirstOrDefault(item =>
                PathsAreEqual(
                    item.MinecraftPath,
                    minecraftPath));

        if (matchingItem is null)
        {
            return;
        }

        InstancesDataGrid.SelectedItem =
            matchingItem;

        InstancesDataGrid.ScrollIntoView(
            matchingItem);
    }

    private void SetBusyState(
        bool isBusy,
        string message)
    {
        StatusTextBlock.Text = message;

        BrowseButton.IsEnabled = !isBusy;
        RefreshButton.IsEnabled = !isBusy;
        InstancesDataGrid.IsEnabled = !isBusy;

        if (isBusy)
        {
            ManagePackButton.IsEnabled = false;
            ScanPackButton.IsEnabled = false;
            CreateVersionButton.IsEnabled = false;
            ViewVersionsButton.IsEnabled = false;
        }
        else
        {
            UpdateActionButtons();
        }
    }

    private void ShowError(
        string heading,
        Exception exception)
    {
        SetBusyState(
            false,
            exception.Message);

        MessageBox.Show(
            $"{heading}{Environment.NewLine}{Environment.NewLine}" +
            exception.Message,
            "ModpackSync",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static bool PathsAreEqual(
        string firstPath,
        string secondPath)
    {
        string firstFullPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(firstPath));

        string secondFullPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(secondPath));

        return firstFullPath.Equals(
            secondFullPath,
            StringComparison.OrdinalIgnoreCase);
    }
}