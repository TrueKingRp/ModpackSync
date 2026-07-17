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
        string versionLabel =
            Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the new version label:",
                $"Create version — {_pack.Name}",
                "1.0.0");

        if (string.IsNullOrWhiteSpace(
                versionLabel))
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
                _pack.Id);

            PackVersion version =
                await _versionManager.CreateVersionAsync(
                    _pack,
                    versionLabel,
                    releaseNotes);

            RefreshVersions();

            VersionsDataGrid.SelectedItem =
                _versions.FirstOrDefault(
                    item =>
                        item.VersionLabel ==
                        version.VersionLabel);

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

    private void RefreshVersions()
    {
        _versions.Clear();

        IReadOnlyList<PackVersion> versions =
            _versionManager.GetVersions(
                _pack.Id);

        foreach (PackVersion version
                 in versions.OrderByDescending(
                     item => item.CreatedAt))
        {
            _versions.Add(
                version);
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