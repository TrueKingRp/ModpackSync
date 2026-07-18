using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ModpackSync.Contracts.Packs;
using ModpackSync.Contracts.Versions;
using ModpackSync.Core.Packs;
using ModpackSync.Core.Versions;

namespace ModpackSync.Desktop.Views;

public partial class VersionsView : UserControl
{
    private readonly ModpackRegistration _pack;
    private readonly PackManager _packManager;
    private readonly VersionManager _versionManager;

    private readonly ObservableCollection<PackVersion> _versions = [];

    private bool _isInitialised;

    public event Action? BackRequested;

    public VersionsView(
        ModpackRegistration pack)
    {
        InitializeComponent();

        _pack = pack;

        _packManager =
            new PackManager();

        _versionManager =
            new VersionManager();

        PackNameTextBlock.Text =
            $"Versions for {_pack.Name}";

        VersionsDataGrid.ItemsSource =
            _versions;

        Loaded += VersionsView_Loaded;
    }

    private async void VersionsView_Loaded(
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
                "Loading versions...");

            await _packManager.InitialiseAsync();
            await _versionManager.InitialiseAsync();

            RefreshVersions();

            SetBusyState(
                false,
                $"Loaded {_versions.Count} versions.");
        }
        catch (Exception ex)
        {
            ShowError(
                "The versions could not be loaded.",
                ex);
        }
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    private void RefreshVersionsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            RefreshVersions();

            StatusTextBlock.Text =
                $"Loaded {_versions.Count} versions.";
        }
        catch (Exception ex)
        {
            ShowError(
                "The versions could not be refreshed.",
                ex);
        }
    }

    private async void CreateVersionButton_Click(
     object sender,
     RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<PackVersion> existingVersions =
                _versionManager.GetVersions(
                    _pack.Id);

            var dialog =
                new CreateVersionWindow(
                    _pack.Name,
                    existingVersions.Select(
                        version =>
                            version.VersionLabel))
                {
                    Owner = Window.GetWindow(this)
                };

            bool? result =
                dialog.ShowDialog();

            if (result != true)
            {
                return;
            }

            var contentDialog =
                new SelectPackContentWindow(
                    _pack.LocalPath)
                {
                    Owner =
                        Window.GetWindow(this)
                };

            bool? contentResult =
                contentDialog.ShowDialog();

            if (contentResult != true)
            {
                StatusTextBlock.Text =
                    "Version creation cancelled.";

                return;
            }

            SetBusyState(
                true,
                $"Creating version {dialog.VersionLabel}...");

            await _packManager.ScanPackAsync(
                _pack.Id,
                contentDialog.Selection);

            PackVersion version =
                await _versionManager.CreateVersionAsync(
                    _pack,
                    dialog.VersionLabel,
                    dialog.ReleaseNotes);

            RefreshVersions();

            VersionsDataGrid.SelectedItem =
                _versions.FirstOrDefault(
                    item =>
                        item.VersionLabel.Equals(
                            version.VersionLabel,
                            StringComparison.OrdinalIgnoreCase));

            if (VersionsDataGrid.SelectedItem is not null)
            {
                VersionsDataGrid.ScrollIntoView(
                    VersionsDataGrid.SelectedItem);
            }

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

    private void VersionsDataGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        PackVersion? selectedVersion =
            VersionsDataGrid.SelectedItem
            as PackVersion;

        bool hasSelection =
            selectedVersion is not null;

        PublishVersionButton.IsEnabled =
            hasSelection &&
            selectedVersion?.IsPublished == false;

        DeleteVersionButton.IsEnabled =
            hasSelection;
    }

    private void PublishVersionButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        PackVersion? selectedVersion =
            VersionsDataGrid.SelectedItem
            as PackVersion;

        if (selectedVersion is null)
        {
            return;
        }

        MessageBox.Show(
            $"Publishing '{selectedVersion.VersionLabel}' " +
            "has not been connected yet.",
            "ModpackSync",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void DeleteVersionButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        PackVersion? selectedVersion =
            VersionsDataGrid.SelectedItem
            as PackVersion;

        if (selectedVersion is null)
        {
            return;
        }

        MessageBoxResult result =
            MessageBox.Show(
                $"Delete version '{selectedVersion.VersionLabel}'?\n\n" +
                "This permanently removes the local version.",
                "Delete Version",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            SetBusyState(
                true,
                $"Deleting {selectedVersion.VersionLabel}...");

            bool removed =
                await _versionManager.RemoveVersionAsync(
                    selectedVersion.Id);

            if (!removed)
            {
                throw new InvalidOperationException(
                    "The version could not be deleted.");
            }

            RefreshVersions();

            SetBusyState(
                false,
                $"Deleted {selectedVersion.VersionLabel}.");
        }
        catch (Exception ex)
        {
            ShowError(
                "Unable to delete version.",
                ex);
        }
    }

    private void RefreshVersions()
    {
        _versions.Clear();

        IReadOnlyList<PackVersion> versions =
            _versionManager.GetVersions(
                _pack.Id);

        foreach (PackVersion version
         in _versionManager
             .GetVersions(_pack.Id)
             .OrderByDescending(v => v.CreatedAt))
        {
            _versions.Add(version);
        }

        VersionsDataGrid.SelectedItem =
            null;

        PublishVersionButton.IsEnabled =
            false;

        DeleteVersionButton.IsEnabled =
            false;
    }

    private void SetBusyState(
        bool isBusy,
        string message)
    {
        StatusTextBlock.Text =
            message;

        RefreshVersionsButton.IsEnabled =
            !isBusy;

        CreateVersionButton.IsEnabled =
            !isBusy;

        VersionsDataGrid.IsEnabled =
            !isBusy;

        if (isBusy)
        {
            PublishVersionButton.IsEnabled =
                false;

            DeleteVersionButton.IsEnabled =
                false;
        }
        else
        {
            VersionsDataGrid_SelectionChanged(
                this,
                null!);
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
}